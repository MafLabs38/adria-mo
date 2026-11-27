using Microsoft.AspNetCore.Identity;

namespace AdriaMo.Api.Data;

/// <summary>An account of the administration (spec §2.1).</summary>
internal sealed class AdriaMoUser : IdentityUser
{
    /// <summary>
    /// Whether the password is a temporary one, given by the super-admin: the account must choose its own before anything else.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
