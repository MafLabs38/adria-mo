using System.Security.Claims;
using AdriaMo.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>Adds to the session whether the account still has a temporary password.</summary>
/// <param name="userManager">The user manager.</param>
/// <param name="roleManager">The role manager.</param>
/// <param name="options">The Identity options.</param>
internal sealed class AccountClaimsFactory(
    UserManager<AdriaMoUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AdriaMoUser, IdentityRole>(userManager, roleManager, options)
{
    /// <summary>The claim present while the password is a temporary one.</summary>
    public const string MustChangePassword = "adriamo:must-change-password";

    /// <inheritdoc />
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AdriaMoUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(MustChangePassword, "true"));
        }

        return identity;
    }
}
