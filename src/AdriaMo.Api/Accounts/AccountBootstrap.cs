using AdriaMo.Api.Data;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>
/// At start, after the database migration: creates the roles, and creates the super-admin from a mounted secret when there is none
/// (spec §2.1). Runs before the server accepts requests.
/// </summary>
/// <param name="services">The root services.</param>
/// <param name="auth">The account settings.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">The logger.</param>
internal sealed partial class AccountBootstrap(
    IServiceProvider services,
    IOptions<AuthOptions> auth,
    TimeProvider time,
    ILogger<AccountBootstrap> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Check(await roles.CreateAsync(new IdentityRole(role)), $"create the role {role}");
            }
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdriaMoUser>>();
        if ((await users.GetUsersInRoleAsync(Roles.SuperAdmin)).Count > 0)
        {
            return;
        }

        var settings = auth.Value;
        if (string.IsNullOrWhiteSpace(settings.BootstrapUserName) || string.IsNullOrWhiteSpace(settings.BootstrapPasswordFile))
        {
            LogNoSuperAdmin(logger);
            return;
        }

        var password = (await File.ReadAllTextAsync(settings.BootstrapPasswordFile, cancellationToken)).TrimEnd('\r', '\n');
        var user = new AdriaMoUser { UserName = settings.BootstrapUserName.Trim(), CreatedAt = time.GetUtcNow() };
        Check(await users.CreateAsync(user, password), "create the super-admin (is its password at least 12 characters?)");
        Check(await users.AddToRoleAsync(user, Roles.SuperAdmin), "give the super-admin its role");
        LogSuperAdminCreated(logger, user.UserName);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No super-admin exists and Auth:BootstrapUserName is not set: nobody can log in.")]
    private static partial void LogNoSuperAdmin(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Super-admin {UserName} created.")]
    private static partial void LogSuperAdminCreated(ILogger logger, string userName);
}
