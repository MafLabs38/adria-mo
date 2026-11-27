using AdriaMo.Core.Constats;

namespace AdriaMo.Core.Accounts;

/// <summary>
/// Pairing a phone by QR code (spec §4.1): the administration shows a one-time code, the phone that scans it is logged in with a
/// session of its own, which the administration can revoke.
/// </summary>
public static class PairingRules
{
    /// <summary>Random bytes of a pairing code: 256 bits, so a code cannot be guessed while it lives.</summary>
    public const int CodeBytes = 32;

    /// <summary>The longest name of a phone.</summary>
    public const int NameMaxLength = 60;

    /// <summary>How long a code shown on the screen can be scanned.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long the session of a paired phone lasts without being used. Longer than a session opened with the password: the phone
    /// sends its photos without its owner typing anything, and pairing again needs a computer.
    /// </summary>
    public static readonly TimeSpan DeviceSessionLifetime = TimeSpan.FromDays(90);

    /// <summary>Checks the name given to a phone ("Téléphone d'Éric").</summary>
    /// <param name="name">The name.</param>
    /// <returns>The problem (<c>Required</c>, <c>TooLong</c>), or <c>null</c>.</returns>
    public static FieldProblem? CheckName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return new FieldProblem("name", "Required");
        }

        return trimmed.Length > NameMaxLength ? new FieldProblem("name", "TooLong") : null;
    }

    /// <summary>Whether a code can still be redeemed.</summary>
    /// <param name="expiresAt">When the code expires.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>Whether it can.</returns>
    public static bool IsAlive(DateTimeOffset expiresAt, DateTimeOffset now) => now < expiresAt;
}
