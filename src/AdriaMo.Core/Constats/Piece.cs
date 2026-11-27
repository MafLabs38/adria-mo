namespace AdriaMo.Core.Constats;

/// <summary>
/// One observation added to a constat (spec §3): a photo, a text, or both. A piece is never changed once added: that is what
/// gives it weight with the landlord.
/// </summary>
public sealed class Piece
{
    /// <summary>The identifier, given by the storage.</summary>
    public int Id { get; set; }

    /// <summary>The constat it belongs to.</summary>
    public int ConstatId { get; set; }

    /// <summary>A comment, or <c>null</c>.</summary>
    public string? Text { get; set; }

    /// <summary>When the photo was taken, read from its EXIF data; <c>null</c> for a text or an undated photo.</summary>
    public DateTimeOffset? TakenAt { get; set; }

    /// <summary>When the piece was added.</summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>The account that added it (never published).</summary>
    public required string AuthorId { get; set; }

    /// <summary>The photo, or <c>null</c> for a text.</summary>
    public PiecePhoto? Photo { get; set; }

    /// <summary>When the defect was seen: when the photo was taken, or when the piece was added (spec §4.1).</summary>
    public DateTimeOffset ObservedAt => Observation.Of(TakenAt, AddedAt);
}

/// <summary>The files of a photo.</summary>
/// <param name="OriginalSha256">The SHA-256 of the original file, as received, in lower-case hexadecimal (the proof).</param>
/// <param name="OriginalFileName">The name of the original in the storage (never published).</param>
/// <param name="PublishedFileName">The name of the reduced copy without metadata (the one published).</param>
/// <param name="ThumbnailFileName">The name of the thumbnail.</param>
/// <param name="Width">The width of the reduced copy, in pixels.</param>
/// <param name="Height">The height of the reduced copy, in pixels.</param>
public sealed record PiecePhoto(
    string OriginalSha256,
    string OriginalFileName,
    string PublishedFileName,
    string ThumbnailFileName,
    int Width,
    int Height);
