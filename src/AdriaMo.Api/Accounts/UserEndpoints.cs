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

    private static async Task<Results<Created<UserSummary>, ValidationProblem, Conflict, ProblemHttpResult>> CreateAsync(
        CreateUserRequest request,
        UserManager<AdriaMoUser> users,
        TimeProvider time)
    {
        var userName = request.UserName?.Trim() ?? "";
        if (userName.Length is < AccountRules.UserNameMinLength or > AccountRules.UserNameMaxLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["userName"] = ["InvalidUserName"] });
        }

        if (await users.FindByNameAsync(userName) is not null)
        {
            return TypedResults.Conflict();
        }

        var user = new AdriaMoUser { UserName = userName, MustChangePassword = true, CreatedAt = time.GetUtcNow() };
        var created = await users.CreateAsync(user, request.TemporaryPassword ?? "");
        if (created.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return TypedResults.Conflict();
        }

        if (!created.Succeeded)
        {
            return Invalid(created);
        }

        if (!(await users.AddToRoleAsync(user, Roles.Admin)).Succeeded)
        {
            // An account without a role must not stay: it could not do anything, and its user name would be taken.
            await users.DeleteAsync(user);
            return TypedResults.Problem("The account could not be given its role.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return TypedResults.Created((string?)null, new UserSummary(user.Id, userName, [Roles.Admin], true, null));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> ResetPasswordAsync(
        string id,
        ResetPasswordRequest request,
        UserManager<AdriaMoUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        // The rules first, so a refused password changes nothing.
        foreach (var validator in users.PasswordValidators)
        {
            var valid = await validator.ValidateAsync(users, user, request.TemporaryPassword);
            if (!valid.Succeeded)
            {
                return Invalid(valid);
            }
        }

        // Temporary before it is set: the password is never usable without the forced change. The reset renews the security stamp,
        // so every session of the account ends; it saves the flag and the unlocking with the new hash.
        user.MustChangePassword = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, request.TemporaryPassword ?? "");
        return reset.Succeeded
            ? TypedResults.NoContent()
            : TypedResults.Problem("The password could not be reset.", statusCode: StatusCodes.Status500InternalServerError);
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
