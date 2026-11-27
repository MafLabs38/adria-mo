namespace AdriaMo.Core.Publishing;

/// <summary>
/// The public data, as published for the public page (spec §10.2, <c>constats.json</c>). Only public constats and what a visitor
/// may see: no account, no author, no original photo, no location metadata.
/// </summary>
/// <param name="Version">The version of this format, raised when it changes in a way the page must know.</param>
/// <param name="PublishedAt">When it was published ("à jour au …").</param>
/// <param name="TimeZone">The IANA time zone of the building, in which the page counts the days.</param>
/// <param name="Constats">The public constats, the most recently changed first.</param>
public sealed record PublicSnapshot(int Version, DateTimeOffset PublishedAt, string TimeZone, IReadOnlyList<PublicConstat> Constats);

/// <summary>A constat as the public sees it.</summary>
/// <param name="Number">Its number.</param>
/// <param name="Title">Its title.</param>
/// <param name="CategoryKey">The key of its category, stable whatever the language or the wording.</param>
/// <param name="Category">The French label of its category.</param>
/// <param name="Location">Where, or <c>null</c>.</param>
/// <param name="Status">Its status, as a key ("open", "resolved").</param>
/// <param name="FirstObservedAt">The first observation, from which the page counts "ouvert depuis N jours".</param>
/// <param name="UpdatedAt">Its last change.</param>
/// <param name="Pieces">Its pieces, oldest first.</param>
public sealed record PublicConstat(
    int Number,
    string Title,
    string CategoryKey,
    string Category,
    string? Location,
    string Status,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PublicPiece> Pieces);

/// <summary>A piece as the public sees it.</summary>
/// <param name="ObservedAt">When the defect was seen.</param>
/// <param name="Text">Its comment, or <c>null</c>.</param>
/// <param name="Photo">Its published photo, or <c>null</c>.</param>
public sealed record PublicPiece(DateTimeOffset ObservedAt, string? Text, PublicPhoto? Photo);

/// <summary>A published photo, by paths relative to the snapshot.</summary>
/// <param name="Src">The reduced copy.</param>
/// <param name="Thumbnail">The thumbnail.</param>
/// <param name="Width">Its width, in pixels.</param>
/// <param name="Height">Its height, in pixels.</param>
public sealed record PublicPhoto(string Src, string Thumbnail, int Width, int Height);
