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
    /// <param name="isAvailable">Whether the files of a photo can be published; a photo whose files are missing is left out
    /// rather than shown broken. All are, by default.</param>
    /// <returns>The snapshot.</returns>
    public static PublicSnapshot Build(
        IEnumerable<Constat> constats,
        DateTimeOffset publishedAt,
        string timeZone,
        Func<PiecePhoto, bool>? isAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(constats);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        var published = constats
            .Where(c => c.IsPublic)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => ToPublic(c, isAvailable ?? (_ => true)))
            .ToArray();
        return new PublicSnapshot(FormatVersion, publishedAt, timeZone, published);
    }

    /// <summary>The names of the photo files a snapshot refers to: the ones to publish, and the only ones to keep published.</summary>
    /// <param name="constats">The constats given to <see cref="Build"/>.</param>
    /// <param name="isAvailable">The same filter as for <see cref="Build"/>.</param>
    /// <returns>The file names.</returns>
    public static IReadOnlySet<string> PublishedFiles(IEnumerable<Constat> constats, Func<PiecePhoto, bool>? isAvailable = null)
    {
        ArgumentNullException.ThrowIfNull(constats);
        return constats
            .Where(c => c.IsPublic)
            .SelectMany(c => c.Pieces)
            .Select(p => p.Photo)
            .OfType<PiecePhoto>()
            .Where(isAvailable ?? (_ => true))
            .SelectMany(p => new[] { p.PublishedFileName, p.ThumbnailFileName })
            .ToHashSet(StringComparer.Ordinal);
    }

    private static PublicConstat ToPublic(Constat constat, Func<PiecePhoto, bool> isAvailable) => new(
        constat.Id,
        constat.Title,
        constat.Category,
        Categories.Find(constat.Category)?.Label ?? constat.Category,
        constat.Location,
        ConstatStatusKey.Of(constat.Status),
        constat.FirstObservedAt,
        constat.UpdatedAt,
        [.. constat.Pieces.OrderBy(p => p.ObservedAt).ThenBy(p => p.Id).Select(p => ToPublic(p, isAvailable))]);

    private static PublicPiece ToPublic(Piece piece, Func<PiecePhoto, bool> isAvailable) => new(
        piece.ObservedAt,
        piece.Text,
        piece.Photo is { } photo && isAvailable(photo)
            ? new PublicPhoto(
                $"{PhotoFolder}/{photo.PublishedFileName}",
                $"{PhotoFolder}/{photo.ThumbnailFileName}",
                photo.Width,
                photo.Height)
            : null);
}
