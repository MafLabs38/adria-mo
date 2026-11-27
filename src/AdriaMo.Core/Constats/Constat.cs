namespace AdriaMo.Core.Constats;

/// <summary>The file of one problem of the building (spec §3): what was seen, where, since when, and its pieces.</summary>
public sealed class Constat
{
    /// <summary>The longest title, in characters.</summary>
    public const int TitleMaxLength = 120;

    /// <summary>The longest location, in characters.</summary>
    public const int LocationMaxLength = 120;

    /// <summary>The longest text of a piece, in characters.</summary>
    public const int TextMaxLength = 2000;

    /// <summary>The number of the constat, given by the storage and shown ("constat n° 12").</summary>
    public int Id { get; set; }

    /// <summary>A short title.</summary>
    public required string Title { get; set; }

    /// <summary>The key of its category (<see cref="Categories"/>).</summary>
    public required string Category { get; set; }

    /// <summary>Where, in words ("hall bât. B"), or <c>null</c>.</summary>
    public string? Location { get; set; }

    /// <summary>Where it stands.</summary>
    public ConstatStatus Status { get; set; } = ConstatStatus.Open;

    /// <summary>Whether the public page shows it.</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When it last changed (a field or a new piece).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Its pieces.</summary>
    public ICollection<Piece> Pieces { get; init; } = [];

    /// <summary>
    /// The first observation (spec §3): the oldest observation among its pieces, not the day it was typed in; its creation when it
    /// has no piece yet.
    /// </summary>
    public DateTimeOffset FirstObservedAt => Pieces.Count == 0 ? CreatedAt : Pieces.Min(p => p.ObservedAt);
}
