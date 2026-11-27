using AdriaMo.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

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
        auth.MapPost("/password", ChangePasswordAsync).RequireAuthorization();
        return api;
    }

    private static async Task<Results<Ok<Me>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<AdriaMoUser> users,
        SignInManager<AdriaMoUser> signIn,
        IPasswordHasher<AdriaMoUser> hasher)
    {
        var user = string.IsNullOrWhiteSpace(request.UserName) ? null : await users.FindByNameAsync(request.UserName.Trim());
        if (user is null)
        {
            // As long as for an existing account, so the time of the answer does not tell which user names exist.
            hasher.VerifyHashedPassword(DummyAccount.User, DummyAccount.Hash(hasher), request.Password ?? "");
            return Refused();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password ?? "", isPersistent: true, lockoutOnFailure: true);
        return result.Succeeded ? TypedResults.Ok(await MeOf(users, user)) : Refused();
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<AdriaMoUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult>> MeAsync(HttpContext http, UserManager<AdriaMoUser> users)
    {
        var user = await users.GetUserAsync(http.User);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await MeOf(users, user));
    }

    private static async Task<Results<Ok<Me>, ValidationProblem, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext http,
        UserManager<AdriaMoUser> users,
        SignInManager<AdriaMoUser> signIn)
    {
        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return Invalid("SameAsCurrent");
        }

        // Checks the current password, the rules of the new one, and renews the security stamp: the other sessions end.
        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword ?? "", request.NewPassword ?? "");
        if (!changed.Succeeded)
        {
            return Invalid([.. changed.Errors.Select(e => e.Code)]);
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);
        return TypedResults.Ok(await MeOf(users, user));
    }

    private static ProblemHttpResult Refused() =>
        TypedResults.Problem(LoginRefused, statusCode: StatusCodes.Status401Unauthorized, title: "Connexion refusée");

    /// <summary>A validation problem on the new password, whose codes the interface says in French.</summary>
    /// <param name="codes">The codes (<see cref="AdriaMo.Core.Accounts.PasswordProblem"/> names, or Identity's).</param>
    /// <returns>The problem.</returns>
    internal static ValidationProblem Invalid(params string[] codes) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = codes });

    private static async Task<Me> MeOf(UserManager<AdriaMoUser> users, AdriaMoUser user) =>
        new(user.UserName ?? "", [.. await users.GetRolesAsync(user)], user.MustChangePassword);

    /// <summary>An account that does not exist, whose hash is computed once, to verify a password against when the user is unknown.</summary>
    private static class DummyAccount
    {
        private static string? _hash;

        public static AdriaMoUser User { get; } = new() { UserName = "-" };

        public static string Hash(IPasswordHasher<AdriaMoUser> hasher) =>
            _hash ??= hasher.HashPassword(User, Guid.NewGuid().ToString("N"));
    }
}

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
internal sealed record Me(string UserName, IReadOnlyList<string> Roles, bool MustChangePassword);
