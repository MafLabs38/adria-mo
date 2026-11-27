using AdriaMo.Core.Constats;

namespace AdriaMo.Core.Photos;

/// <summary>
/// A photo in the inbox "À classer" (spec §4.1): shared from the gallery or sent later by the phone, not yet attached to a
/// constat. Its files are those of a piece already: attaching it only moves it into a constat.
/// </summary>
public sealed class InboxPhoto
{
    /// <summary>The identifier, given by the storage.</summary>
    public int Id { get; set; }

    /// <summary>The files of the photo.</summary>
    public required PiecePhoto Photo { get; set; }

    /// <summary>When the photo was taken, read from its EXIF data; <c>null</c> for an undated photo.</summary>
    public DateTimeOffset? TakenAt { get; set; }

    /// <summary>When the photo reached the back end: the date of deposit of the piece it becomes.</summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>The account that sent it.</summary>
    public required string AuthorId { get; set; }

    /// <summary>
    /// Whether the photo has a date to sort and group it by: its own, unless after its upload, which only a wrong camera clock
    /// gives (<see cref="Observation"/>).
    /// </summary>
    public bool IsDated => Observation.IsDated(TakenAt, AddedAt);

    /// <summary>When the defect was seen: the order in which photos are attached (<see cref="Observation"/>).</summary>
    public DateTimeOffset ObservedAt => Observation.Of(TakenAt, AddedAt);

    /// <summary>The piece this photo becomes in a constat: same files, same dates, same author.</summary>
    /// <param name="constatId">The constat.</param>
    /// <param name="text">The comment that goes with it, or <c>null</c>.</param>
    /// <returns>The piece.</returns>
    public Piece ToPiece(int constatId, string? text) => new()
    {
        ConstatId = constatId,
        Text = text,
        TakenAt = TakenAt,
        AddedAt = AddedAt,
        AuthorId = AuthorId,
        // A copy: the piece and the photo of the inbox each own their files' record.
        Photo = Photo with { },
    };
}
