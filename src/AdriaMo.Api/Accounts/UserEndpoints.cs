using AdriaMo.Api.Data;
using AdriaMo.Core.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdriaMo.Api.Accounts;

/// <summary>The accounts, managed by the super-admin (spec §2.1): create an administrator, reset a forgotten password.</summary>
internal static class UserEndpoints
{
    /// <summary>Maps <c>/users/*</c>.</summary>
    /// <param name="api">The <c>/api/v1</c> group.</param>
    /// <returns>The group, for chaining.</returns>
    public static RouteGroupBuilder MapUsers(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").RequireAuthorization(AccountsSetup.SuperAdminPolicy);
        users.MapGet("/", ListAsync);
        users.MapPost("/", CreateAsync);
        users.MapPost("/{id}/password", ResetPasswordAsync);
        return api;
    }

    private static async Task<Ok<UserSummary[]>> ListAsync(UserManager<AdriaMoUser> users, TimeProvider time)
    {
        var all = await users.Users.OrderBy(u => u.UserName).ToListAsync();
        var now = time.GetUtcNow();
        var summaries = new List<UserSummary>(all.Count);
        foreach (var user in all)
        {
            summaries.Add(new UserSummary(
                user.Id,
                user.UserName ?? "",
                [.. await users.GetRolesAsync(user)],
                user.MustChangePassword,
                user.LockoutEnd > now ? user.LockoutEnd : null));
        }

        return TypedResults.Ok(summaries.ToArray());
    }

    private static async Task<Results<Created<UserSummary>, ValidationProblem, Conflict>> CreateAsync(
        CreateUserRequest request,
        UserManager<AdriaMoUser> users,
        TimeProvider time)
    {
        var userName = request.UserName?.Trim() ?? "";
        if (userName.Length is < 2 or > 64)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["userName"] = ["InvalidUserName"] });
        }

        if (await users.FindByNameAsync(userName) is not null)
        {
            return TypedResults.Conflict();
        }

        var user = new AdriaMoUser { UserName = userName, MustChangePassword = true, CreatedAt = time.GetUtcNow() };
        var created = await users.CreateAsync(user, request.TemporaryPassword ?? "");
        if (!created.Succeeded)
        {
            return Invalid(created);
        }

        await users.AddToRoleAsync(user, Roles.Admin);
        return TypedResults.Created($"/api/v1/users/{user.Id}", new UserSummary(user.Id, userName, [Roles.Admin], true, null));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ResetPasswordAsync(
        string id,
        ResetPasswordRequest request,
        UserManager<AdriaMoUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        // Checks the rules and renews the security stamp: every session of the account ends.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, request.TemporaryPassword ?? "");
        if (!reset.Succeeded)
        {
            return Invalid(reset);
        }

        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return TypedResults.NoContent();
    }

    private static ValidationProblem Invalid(IdentityResult result) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["temporaryPassword"] = [.. result.Errors.Select(e => e.Code)],
        });
}

/// <summary>A new administrator.</summary>
/// <param name="UserName">Its user name.</param>
/// <param name="TemporaryPassword">The temporary password the super-admin gives it.</param>
internal sealed record CreateUserRequest(string? UserName, string? TemporaryPassword);

/// <summary>A reset of a forgotten password.</summary>
/// <param name="TemporaryPassword">The new temporary password.</param>
internal sealed record ResetPasswordRequest(string? TemporaryPassword);

/// <summary>An account as the super-admin sees it (never its password nor its hash).</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="UserName">Its user name.</param>
/// <param name="Roles">Its roles.</param>
/// <param name="MustChangePassword">Whether its password is still a temporary one.</param>
/// <param name="LockedOutUntil">Until when it is locked after failed attempts, or <c>null</c>.</param>
internal sealed record UserSummary(string Id, string UserName, IReadOnlyList<string> Roles, bool MustChangePassword, DateTimeOffset? LockedOutUntil);
