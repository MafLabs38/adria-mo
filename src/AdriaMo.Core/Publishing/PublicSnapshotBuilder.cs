using AdriaMo.Core.Constats;

namespace AdriaMo.Core.Publishing;

/// <summary>Turns the constats into the public snapshot: the rule of what leaves the back end (spec §10.2).</summary>
public static class PublicSnapshotBuilder
{
    /// <summary>The version of the published format.</summary>
    public const int FormatVersion = 1;

    /// <summary>The folder of the published photos, relative to the snapshot.</summary>
    public const string PhotoFolder = "photos";

    /// <summary>Builds the snapshot.</summary>
    /// <param name="constats">All the constats, with their pieces; the private ones are left out here.</param>
    /// <param name="publishedAt">The instant of the publication.</param>
    /// <param name="timeZone">The IANA time zone of the building.</param>
    /// <returns>The snapshot.</returns>
    public static PublicSnapshot Build(IEnumerable<Constat> constats, DateTimeOffset publishedAt, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(constats);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        var published = constats
            .Where(c => c.IsPublic)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Select(ToPublic)
            .ToArray();
        return new PublicSnapshot(FormatVersion, publishedAt, timeZone, published);
    }

    /// <summary>The names of the photo files a snapshot refers to: the ones to publish, and the only ones to keep published.</summary>
    /// <param name="constats">The constats given to <see cref="Build"/>.</param>
    /// <returns>The file names.</returns>
    public static IReadOnlySet<string> PublishedFiles(IEnumerable<Constat> constats)
    {
        ArgumentNullException.ThrowIfNull(constats);
        return constats
            .Where(c => c.IsPublic)
            .SelectMany(c => c.Pieces)
            .Select(p => p.Photo)
            .OfType<PiecePhoto>()
            .SelectMany(p => new[] { p.PublishedFileName, p.ThumbnailFileName })
            .ToHashSet(StringComparer.Ordinal);
    }

    private static PublicConstat ToPublic(Constat constat) => new(
        constat.Id,
        constat.Title,
        Categories.Find(constat.Category)?.Label ?? constat.Category,
        constat.Location,
        ConstatStatusKey.Of(constat.Status),
        constat.FirstObservedAt,
        constat.UpdatedAt,
        [.. constat.Pieces.OrderBy(p => p.ObservedAt).ThenBy(p => p.Id).Select(ToPublic)]);

    private static PublicPiece ToPublic(Piece piece) => new(
        piece.ObservedAt,
        piece.Text,
        piece.Photo is { } photo
            ? new PublicPhoto(
                $"{PhotoFolder}/{photo.PublishedFileName}",
                $"{PhotoFolder}/{photo.ThumbnailFileName}",
                photo.Width,
                photo.Height)
            : null);
}
