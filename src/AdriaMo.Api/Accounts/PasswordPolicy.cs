using AdriaMo.Api.Data;
using AdriaMo.Core.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AdriaMo.Api.Accounts;

/// <summary>Applies the rules of <see cref="PasswordRules"/> to every new password Identity sets (creation, change, reset).</summary>
internal sealed class AdriaMoPasswordValidator : IPasswordValidator<AdriaMoUser>
{
    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(UserManager<AdriaMoUser> manager, AdriaMoUser user, string? password)
    {
        var problems = PasswordRules.Check(password ?? "", user.UserName ?? "");
        return Task.FromResult(problems.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed([.. problems.Select(p => new IdentityError { Code = p.ToString(), Description = p.ToString() })]));
    }
}

/// <summary>
/// The hasher of Identity (PBKDF2-HMAC-SHA512 with a salt per account), fed with the normalized password (spec §2.1): the same
/// password typed on two keyboards gives the same hash.
/// </summary>
/// <param name="options">The hasher options (<see cref="PasswordPolicy.Iterations"/>).</param>
internal sealed class NormalizingPasswordHasher(IOptions<PasswordHasherOptions> options) : IPasswordHasher<AdriaMoUser>
{
    private readonly PasswordHasher<AdriaMoUser> _inner = new(options);

    /// <inheritdoc />
    public string HashPassword(AdriaMoUser user, string password) => _inner.HashPassword(user, PasswordRules.Normalize(password));

    /// <inheritdoc />
    public PasswordVerificationResult VerifyHashedPassword(AdriaMoUser user, string hashedPassword, string providedPassword) =>
        _inner.VerifyHashedPassword(user, hashedPassword, PasswordRules.Normalize(providedPassword));
}

/// <summary>The numbers of the password policy that are not in <see cref="PasswordRules"/>.</summary>
internal static class PasswordPolicy
{
    /// <summary>PBKDF2-HMAC-SHA512 iterations, as OWASP recommends; Identity rehashes an older hash at the next login.</summary>
    public const int Iterations = 210_000;

    /// <summary>Failed attempts before the account is locked.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>How long a session lasts without being used.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
}
