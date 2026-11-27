namespace AdriaMo.Api.Data;

/// <summary>
/// A phone paired by QR code (spec §4.1). While its code waits to be scanned the record holds the hash of the code, never the code;
/// once scanned, the phone's session carries the identifier of this record, and the session ends when the record is revoked.
/// </summary>
internal sealed class PairedDevice
{
    /// <summary>The identifier, carried by the phone's session.</summary>
    public required string Id { get; set; }

    /// <summary>The account the phone acts for.</summary>
    public required string UserId { get; set; }

    /// <summary>The name its owner gave it.</summary>
    public required string Name { get; set; }

    /// <summary>When the code was shown.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The SHA-256 of the pairing code, until it is scanned.</summary>
    public string? CodeHash { get; set; }

    /// <summary>Until when the code can be scanned.</summary>
    public DateTimeOffset CodeExpiresAt { get; set; }

    /// <summary>The security stamp of the account when the code was shown: a password changed since then makes the code void.</summary>
    public required string SecurityStamp { get; set; }

    /// <summary>When the phone scanned the code, or <c>null</c>.</summary>
    public DateTimeOffset? PairedAt { get; set; }

    /// <summary>When the phone was revoked, or <c>null</c>.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
