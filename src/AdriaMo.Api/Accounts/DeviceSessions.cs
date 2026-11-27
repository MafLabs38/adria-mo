using System.Security.Claims;
using AdriaMo.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdriaMo.Api.Accounts;

/// <summary>The sessions of paired phones (spec §4.1): they carry the phone's identifier and end when the phone is revoked.</summary>
internal static class DeviceSessions
{
    /// <summary>The claim of a phone's session: the identifier of its <see cref="PairedDevice"/>.</summary>
    public const string DeviceClaim = "adriamo:device";

    /// <summary>The phone of a session, or <c>null</c> for a session opened with the password.</summary>
    /// <param name="user">The session.</param>
    /// <returns>The identifier of the phone.</returns>
    public static string? DeviceOf(ClaimsPrincipal user) => user.FindFirstValue(DeviceClaim);

    /// <summary>
    /// Runs after the check of the security stamp, at every request: the session of a revoked phone ends there.
    /// </summary>
    /// <param name="context">The cookie being validated.</param>
    /// <returns>The task.</returns>
    public static async Task RejectRevokedAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal is not { } principal || DeviceOf(principal) is not { } device)
        {
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = context.HttpContext.RequestServices.GetRequiredService<AdriaMoDbContext>();
        var paired = await db.Devices.AnyAsync(d => d.Id == device && d.UserId == userId && d.PairedAt != null && d.RevokedAt == null);
        if (!paired)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }

    /// <summary>Keeps the phone's identifier when the session is rebuilt after the check of the security stamp.</summary>
    /// <param name="context">The old and the new session.</param>
    /// <returns>The task.</returns>
    public static Task KeepDevice(SecurityStampRefreshingPrincipalContext context)
    {
        if (context.CurrentPrincipal is { } current && DeviceOf(current) is { } device && context.NewPrincipal?.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(DeviceClaim, device));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Renews the current session after its password changed (the security stamp changed), keeping its expiry and its phone: what
    /// <see cref="SignInManager{TUser}.RefreshSignInAsync"/> does, without dropping the phone.
    /// </summary>
    /// <param name="http">The request.</param>
    /// <param name="signIn">The sign-in manager.</param>
    /// <param name="user">The account.</param>
    /// <returns>The task.</returns>
    public static async Task RenewAsync(HttpContext http, SignInManager<AdriaMoUser> signIn, AdriaMoUser user)
    {
        var current = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        Claim[] claims = DeviceOf(http.User) is { } device ? [new Claim(DeviceClaim, device)] : [];
        await signIn.SignInWithClaimsAsync(user, current.Properties, claims);
    }

    /// <summary>
    /// Revokes the phones of an account, but the one in use, when its password changes: their sessions already ended with the
    /// security stamp, and the list must not show them as paired.
    /// </summary>
    /// <param name="db">The database.</param>
    /// <param name="userId">The account.</param>
    /// <param name="except">The phone that stays, or <c>null</c>.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The task.</returns>
    public static Task RevokeAllAsync(AdriaMoDbContext db, string userId, string? except, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Devices
            .Where(d => d.UserId == userId && d.RevokedAt == null && d.Id != except)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.RevokedAt, now).SetProperty(d => d.CodeHash, (string?)null), cancellationToken);
}
