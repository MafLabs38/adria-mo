using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AdriaMo.Api.Data;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>
/// Pairing a phone by QR code (spec §4.1): an administrator asks for a code on a computer, the phone that scans it is logged in for
/// that account without typing the password, and the administrator can revoke it.
/// </summary>
internal static class DeviceEndpoints
{
    /// <summary>The same answer whatever is wrong with a code: unknown, expired, already used, or its account changed.</summary>
    private const string PairingRefused = "Ce code d'appairage n'est plus valable : affichez-en un nouveau sur l'ordinateur.";

    /// <summary>Maps <c>/devices/*</c> and <c>/auth/pair</c>.</summary>
    /// <param name="api">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapDevices(this RouteGroupBuilder api)
    {
        var devices = api.MapGroup("/devices").RequireAuthorization(AccountsSetup.EditorPolicy);
        devices.MapGet("/", ListAsync);
        devices.MapPost("/", CreateAsync);
        devices.MapDelete("/{id}", RevokeAsync);
        // Without a session (the phone has none yet), limited like the login.
        api.MapPost("/auth/pair", PairAsync).RequireRateLimiting(AccountsSetup.LoginLimiter);
        return api;
    }

    private static async Task<Ok<DeviceSummary[]>> ListAsync(
        HttpContext http,
        UserManager<AdriaMoUser> users,
        AdriaMoDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(http.User);
        var current = DeviceSessions.DeviceOf(http.User);
        var now = time.GetUtcNow();
        var devices = await db.Devices.AsNoTracking()
            .Where(d => d.UserId == userId && d.RevokedAt == null && (d.PairedAt != null || d.CodeExpiresAt > now))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(devices
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DeviceSummary(d.Id, d.Name, d.CreatedAt, d.PairedAt, d.Id == current))
            .ToArray());
    }

    private static async Task<Results<Created<PairingCode>, ValidationProblem, UnauthorizedHttpResult, ForbidHttpResult>> CreateAsync(
        CreateDeviceRequest request,
        HttpContext http,
        UserManager<AdriaMoUser> users,
        AdriaMoDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (PairingRules.CheckName(request.Name) is { } problem)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [problem.Field] = [problem.Code] });
        }

        // Only a session opened with the password pairs a phone: a stolen phone cannot pair another device before it is removed.
        if (DeviceSessions.DeviceOf(http.User) is not null)
        {
            return TypedResults.Forbid();
        }

        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        // One code at a time per account: a code shown and not scanned is dropped when another is asked for, and so are the
        // phones removed earlier, which are kept for nothing.
        await db.Devices.Where(d => d.UserId == user.Id && (d.PairedAt == null || d.RevokedAt != null)).ExecuteDeleteAsync(cancellationToken);

        var code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(PairingRules.CodeBytes));
        var now = time.GetUtcNow();
        var device = new PairedDevice
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            Name = request.Name!.Trim(),
            CreatedAt = now,
            CodeHash = Hash(code),
            CodeExpiresAt = now + PairingRules.CodeLifetime,
            SecurityStamp = await users.GetSecurityStampAsync(user),
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created((string?)null, new PairingCode(device.Id, code, device.CodeExpiresAt));
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(
        string id,
        HttpContext http,
        UserManager<AdriaMoUser> users,
        AdriaMoDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(http.User);
        var revoked = await db.Devices
            .Where(d => d.Id == id && d.UserId == userId && d.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.RevokedAt, time.GetUtcNow()).SetProperty(d => d.CodeHash, (string?)null), cancellationToken);
        return revoked == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Me>, ProblemHttpResult>> PairAsync(
        PairRequest request,
        [AsParameters] AccountServices services,
        CancellationToken cancellationToken)
    {
        var (users, signIn, db, time, building) = services;
        if (string.IsNullOrEmpty(request.Code) || request.Code.Length > 64)
        {
            return Refused();
        }

        var now = time.GetUtcNow();
        var hash = Hash(request.Code);
        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.CodeHash == hash && d.PairedAt == null && d.RevokedAt == null, cancellationToken);
        var user = device is null ? null : await users.FindByIdAsync(device.UserId);
        if (device is null
            || user is null
            || !PairingRules.IsAlive(device.CodeExpiresAt, now)
            || await users.IsLockedOutAsync(user)
            || !string.Equals(await users.GetSecurityStampAsync(user), device.SecurityStamp, StringComparison.Ordinal))
        {
            return Refused();
        }

        // Used once: the code is claimed by one conditional write, so two scans at the same instant cannot both open a session.
        var claimed = await db.Devices
            .Where(d => d.Id == device.Id && d.CodeHash == hash && d.PairedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.CodeHash, (string?)null).SetProperty(d => d.PairedAt, now), cancellationToken);
        if (claimed != 1)
        {
            return Refused();
        }

        var properties = new AuthenticationProperties { IsPersistent = true, IssuedUtc = now, ExpiresUtc = now + PairingRules.DeviceSessionLifetime };
        await signIn.SignInWithClaimsAsync(user, properties, [new Claim(DeviceSessions.DeviceClaim, device.Id)]);
        return TypedResults.Ok(new Me(user.UserName ?? "", [.. await users.GetRolesAsync(user)], user.MustChangePassword, building.Value.TimeZone));
    }

    private static ProblemHttpResult Refused() =>
        TypedResults.Problem(PairingRefused, statusCode: StatusCodes.Status401Unauthorized, title: "Appairage refusé");

    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}

/// <summary>A phone to pair.</summary>
/// <param name="Name">The name its owner gives it.</param>
internal sealed record CreateDeviceRequest(string? Name);

/// <summary>The code to show as a QR code, once: it is never stored nor shown again.</summary>
/// <param name="Id">The phone.</param>
/// <param name="Code">The code.</param>
/// <param name="ExpiresAt">Until when it can be scanned.</param>
internal sealed record PairingCode(string Id, string Code, DateTimeOffset ExpiresAt);

/// <summary>The code scanned by the phone.</summary>
/// <param name="Code">The code.</param>
internal sealed record PairRequest(string? Code);

/// <summary>A phone of the account: paired, or waiting for its code to be scanned.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Name">Its name.</param>
/// <param name="CreatedAt">When its code was shown.</param>
/// <param name="PairedAt">When it was paired, or <c>null</c> while the code waits.</param>
/// <param name="IsCurrent">Whether it is the phone making the request.</param>
internal sealed record DeviceSummary(string Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? PairedAt, bool IsCurrent);
