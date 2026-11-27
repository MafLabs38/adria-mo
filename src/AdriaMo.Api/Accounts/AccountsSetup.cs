using System.Threading.RateLimiting;
using AdriaMo.Api.Data;
using AdriaMo.Api.Settings;
using AdriaMo.Core.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>The accounts: Identity, the session cookie, the authorization policies and the limit of login attempts (spec §2.1).</summary>
internal static class AccountsSetup
{
    /// <summary>Content of the administration: an administrator or the super-admin whose password is not a temporary one.</summary>
    public const string EditorPolicy = "editor";

    /// <summary>Accounts and settings: the super-admin whose password is not a temporary one.</summary>
    public const string SuperAdminPolicy = "super-admin";

    /// <summary>The rate limiter of the login endpoint.</summary>
    public const string LoginLimiter = "login";

    /// <summary>Registers the accounts.</summary>
    /// <param name="services">The services.</param>
    /// <returns>The services, for chaining.</returns>
    public static IServiceCollection AddAdriaMoAccounts(this IServiceCollection services)
    {
        services.Configure<PasswordHasherOptions>(o => o.IterationCount = AccountRules.HashIterations);
        services.AddScoped<IPasswordHasher<AdriaMoUser>, NormalizingPasswordHasher>();

        services.AddIdentityCore<AdriaMoUser>(o =>
            {
                // The length and the other rules are PasswordRules' (AdriaMoPasswordValidator); no composition rule.
                o.Password.RequiredLength = 0;
                o.Password.RequiredUniqueChars = 0;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = AccountRules.MaxFailedAttempts;
                o.Lockout.DefaultLockoutTimeSpan = AccountRules.LockoutDuration;
                o.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AdriaMoDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<AccountClaimsFactory>()
            .AddPasswordValidator<AdriaMoPasswordValidator>();

        // A changed or reset password ends every other session at its next request.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure(cookie =>
            {
                // HTTPS only, always: the administration is reached by HTTPS, also in the sandbox.
                cookie.Cookie.Name = "__Host-adriamo";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.Path = "/";
                cookie.ExpireTimeSpan = AccountRules.SessionLifetime;
                cookie.SlidingExpiration = true;
                // An API answers with a status, never with a redirection to a login page.
                cookie.Events.OnRedirectToLogin = context => Reject(context.Response, StatusCodes.Status401Unauthorized);
                cookie.Events.OnRedirectToAccessDenied = context => Reject(context.Response, StatusCodes.Status403Forbidden);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(EditorPolicy, policy => policy
                .RequireRole(Roles.SuperAdmin, Roles.Admin)
                .RequireAssertion(context => !context.User.HasClaim(c => c.Type == AccountClaimsFactory.MustChangePassword)))
            .AddPolicy(SuperAdminPolicy, policy => policy
                .RequireRole(Roles.SuperAdmin)
                .RequireAssertion(context => !context.User.HasClaim(c => c.Type == AccountClaimsFactory.MustChangePassword)));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginLimiter, context =>
            {
                var perMinute = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginAttemptsPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
        });

        services.AddHostedService<AccountBootstrap>();
        return services;
    }

    private static Task Reject(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}
