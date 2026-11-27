using AdriaMo.Api.Data;
using AdriaMo.Api.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>Logging in and out, and one's own password (spec §2.1).</summary>
internal static class AccountEndpoints
{
    /// <summary>The same answer whether the user name exists, the password is wrong or the account is locked.</summary>
    private const string LoginRefused = "Identifiant ou mot de passe incorrect, ou compte bloqué quelques minutes après plusieurs essais.";

    /// <summary>Maps <c>/auth/*</c>.</summary>
    /// <param name="api">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapAccounts(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");
        auth.MapPost("/login", LoginAsync).RequireRateLimiting(AccountsSetup.LoginLimiter);
        auth.MapPost("/logout", LogoutAsync);
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        // Limited like the login: a borrowed session must not try current passwords at full speed.
        auth.MapPost("/password", ChangePasswordAsync).RequireAuthorization().RequireRateLimiting(AccountsSetup.LoginLimiter);
        return api;
    }

    private static async Task<Results<Ok<Me>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<AdriaMoUser> users,
        SignInManager<AdriaMoUser> signIn,
        IPasswordHasher<AdriaMoUser> hasher,
        IOptions<BuildingOptions> building)
    {
        var user = string.IsNullOrWhiteSpace(request.UserName) ? null : await users.FindByNameAsync(request.UserName.Trim());
        if (user is null)
        {
            // As long as for an existing account, so the time of the answer does not tell which user names exist.
            hasher.VerifyHashedPassword(DummyAccount.User, DummyAccount.Hash(hasher), request.Password ?? "");
            return Refused();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password ?? "", isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            // Identity refuses a locked account without hashing: hash anyway, so the time does not tell that the account exists.
            hasher.VerifyHashedPassword(DummyAccount.User, DummyAccount.Hash(hasher), request.Password ?? "");
        }

        return result.Succeeded ? TypedResults.Ok(await MeOf(users, user, building)) : Refused();
    }

    private static async Task<NoContent> LogoutAsync(
        HttpContext http,
        SignInManager<AdriaMoUser> signIn,
        AdriaMoDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // A phone that logs out is unpaired: its session would not come back without a new code anyway.
        if (DeviceSessions.DeviceOf(http.User) is { } device)
        {
            await db.Devices.Where(d => d.Id == device && d.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.RevokedAt, time.GetUtcNow()), cancellationToken);
        }

        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult>> MeAsync(
        HttpContext http,
        UserManager<AdriaMoUser> users,
        IOptions<BuildingOptions> building)
    {
        var user = await users.GetUserAsync(http.User);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await MeOf(users, user, building));
    }

    private static async Task<Results<Ok<Me>, ValidationProblem, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext http,
        [AsParameters] PasswordServices services,
        CancellationToken cancellationToken)
    {
        var (users, signIn, db, time, building) = services;
        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return Invalid("SameAsCurrent");
        }

        // Checks the current password and the rules of the new one, renews the security stamp (the other sessions end) and saves the
        // end of the temporary password with the new hash, in one write.
        user.MustChangePassword = false;
        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword ?? "", request.NewPassword ?? "");
        if (!changed.Succeeded)
        {
            return Invalid([.. changed.Errors.Select(e => e.Code)]);
        }

        // The other sessions end with the security stamp; the other phones are revoked so the list does not show them as paired.
        await DeviceSessions.RevokeAllAsync(db, user.Id, DeviceSessions.DeviceOf(http.User), time.GetUtcNow(), cancellationToken);
        await DeviceSessions.RenewAsync(http, signIn, user);
        return TypedResults.Ok(await MeOf(users, user, building));
    }

    private static ProblemHttpResult Refused() =>
        TypedResults.Problem(LoginRefused, statusCode: StatusCodes.Status401Unauthorized, title: "Connexion refusée");

    private static ValidationProblem Invalid(params string[] codes) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = codes });

    private static async Task<Me> MeOf(UserManager<AdriaMoUser> users, AdriaMoUser user, IOptions<BuildingOptions> building) =>
        new(user.UserName ?? "", [.. await users.GetRolesAsync(user)], user.MustChangePassword, building.Value.TimeZone);

    /// <summary>An account that does not exist, whose hash is computed once, to verify a password against when the user is unknown.</summary>
    private static class DummyAccount
    {
        private static string? _hash;

        public static AdriaMoUser User { get; } = new() { UserName = "-" };

        public static string Hash(IPasswordHasher<AdriaMoUser> hasher) =>
            _hash ??= hasher.HashPassword(User, Guid.NewGuid().ToString("N"));
    }
}

/// <summary>What changing one's password needs.</summary>
/// <param name="Users">The accounts.</param>
/// <param name="SignIn">The sign-in manager.</param>
/// <param name="Db">The database, for the paired phones.</param>
/// <param name="Time">The clock.</param>
/// <param name="Building">The building settings.</param>
internal sealed record PasswordServices(
    UserManager<AdriaMoUser> Users,
    SignInManager<AdriaMoUser> SignIn,
    AdriaMoDbContext Db,
    TimeProvider Time,
    IOptions<BuildingOptions> Building);

/// <summary>A login.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="Password">The password.</param>
internal sealed record LoginRequest(string? UserName, string? Password);

/// <summary>A change of one's own password.</summary>
/// <param name="CurrentPassword">The current password.</param>
/// <param name="NewPassword">The new password.</param>
internal sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>The logged-in account.</summary>
/// <param name="UserName">Its user name.</param>
/// <param name="Roles">Its roles.</param>
/// <param name="MustChangePassword">Whether it must choose a password before anything else.</param>
/// <param name="TimeZone">The time zone of the building, in which the administration shows dates.</param>
internal sealed record Me(string UserName, IReadOnlyList<string> Roles, bool MustChangePassword, string TimeZone);
