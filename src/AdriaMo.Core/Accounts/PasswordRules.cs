using System.Text;

namespace AdriaMo.Core.Accounts;

/// <summary>
/// The rules of a new password (spec §2.1, after NIST SP 800-63B and OWASP): a long password rather than a complicated one. No
/// rule on upper case, digits or symbols, no periodic expiry; only a length, and a refusal of what is guessed first.
/// </summary>
public static class PasswordRules
{
    /// <summary>The shortest password accepted, in characters.</summary>
    public const int MinimumLength = 12;

    /// <summary>The longest password accepted, in characters: long enough for any sentence, short enough not to be an attack on the hash.</summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// Passwords of twelve characters or more that are tried first by guessing tools (the shorter ones are refused by the length
    /// already). Compared without case.
    /// </summary>
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "12345678901234", "123456123456", "111111111111", "000000000000",
        "qwertyuiopas", "qwerty123456", "123456qwerty", "azertyuiopqs", "azerty123456", "123456azerty",
        "azertyuiop12", "azertyuiop123", "password1234", "password12345", "passwordpassword", "motdepasse12",
        "motdepasse123", "motdepasse1234", "1q2w3e4r5t6y", "qazwsxedcrfv", "iloveyou1234", "jetaime123456",
        "soleil123456", "bonjour123456", "adminadmin12", "administrator", "administrateur", "changeme1234",
        "welcome12345", "letmein12345", "abcdefghijkl", "abcdef123456", "0123456789ab", "aaaaaa111111",
    };

    /// <summary>
    /// The form a password is checked and hashed in: Unicode NFKC, so the same password typed on two keyboards (an "é" as one
    /// character or as "e" plus an accent) is the same password.
    /// </summary>
    /// <param name="password">The password as typed.</param>
    /// <returns>Its normalized form.</returns>
    public static string Normalize(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return password.Normalize(NormalizationForm.FormKC);
    }

    /// <summary>Checks a new password.</summary>
    /// <param name="password">The password as typed.</param>
    /// <param name="userName">The user name of the account, which the password must not contain.</param>
    /// <returns>The problems found; empty when the password is accepted.</returns>
    public static IReadOnlyList<PasswordProblem> Check(string password, string userName)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(userName);

        var normalized = Normalize(password);
        var characters = normalized.EnumerateRunes().ToArray();
        var problems = new List<PasswordProblem>();

        if (characters.Length < MinimumLength)
        {
            problems.Add(PasswordProblem.TooShort);
        }
        else if (characters.Length > MaximumLength)
        {
            problems.Add(PasswordProblem.TooLong);
        }

        if (characters.Length > 1 && characters.All(c => c == characters[0]))
        {
            problems.Add(PasswordProblem.OneRepeatedCharacter);
        }

        var name = Normalize(userName).Trim();
        if (name.Length > 0 && normalized.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(PasswordProblem.ContainsUserName);
        }

        if (Common.Contains(normalized.Trim()))
        {
            problems.Add(PasswordProblem.TooCommon);
        }

        return problems;
    }
}
