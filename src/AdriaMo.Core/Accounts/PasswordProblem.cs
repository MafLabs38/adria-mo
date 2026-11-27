namespace AdriaMo.Core.Accounts;

/// <summary>Why a new password is refused (spec §2.1). The name is the code the API returns; the interface says it in French.</summary>
public enum PasswordProblem
{
    /// <summary>Fewer characters than <see cref="PasswordRules.MinimumLength"/>.</summary>
    TooShort,

    /// <summary>More characters than <see cref="PasswordRules.MaximumLength"/>.</summary>
    TooLong,

    /// <summary>One character repeated, such as "aaaaaaaaaaaa".</summary>
    OneRepeatedCharacter,

    /// <summary>The password contains the account's user name.</summary>
    ContainsUserName,

    /// <summary>The password is in the list of passwords that are tried first.</summary>
    TooCommon,
}
