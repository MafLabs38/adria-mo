namespace AdriaMo.Core.Accounts;

/// <summary>The numbers of the account rules (spec §2.1) that are not about the password itself (<see cref="PasswordRules"/>).</summary>
public static class AccountRules
{
    /// <summary>PBKDF2-HMAC-SHA512 iterations of a password hash, as OWASP recommends.</summary>
    public const int HashIterations = 210_000;

    /// <summary>Failed attempts before the account is locked.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>The shortest user name: a password must not contain it, so two letters would refuse too many sentences.</summary>
    public const int UserNameMinLength = 3;

    /// <summary>The longest user name.</summary>
    public const int UserNameMaxLength = 64;

    /// <summary>How long a locked account stays locked.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>How long a session lasts without being used.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
}
