namespace AdriaMo.Core.Photos;

/// <summary>
/// Photos taken a few minutes apart, most likely of the same defect (spec §4.1): the inbox offers to attach a whole series to one
/// constat at once.
/// </summary>
public sealed class PhotoSeries
{
    /// <summary>Creates a series.</summary>
    /// <param name="photos">The photos of the series: at least one, oldest first. The list is copied.</param>
    public PhotoSeries(IEnumerable<TakenPhoto> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        var copy = photos.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A series holds at least one photo.", nameof(photos));
        }

        for (var i = 1; i < copy.Length; i++)
        {
            if (copy[i].TakenAt < copy[i - 1].TakenAt)
            {
                throw new ArgumentException("The photos of a series are ordered, oldest first.", nameof(photos));
            }
        }

        Photos = copy;
    }

    /// <summary>The photos of the series, oldest first.</summary>
    public IReadOnlyList<TakenPhoto> Photos { get; }

    /// <summary>When the first photo of the series was taken.</summary>
    public DateTimeOffset StartedAt => Photos[0].TakenAt;

    /// <summary>When the last photo of the series was taken.</summary>
    public DateTimeOffset EndedAt => Photos[^1].TakenAt;
}

/// <summary>A photo and the moment it was taken (read from its EXIF data, not the moment it was uploaded).</summary>
/// <param name="Id">The identifier of the photo.</param>
/// <param name="TakenAt">When the photo was taken.</param>
public sealed record TakenPhoto(string Id, DateTimeOffset TakenAt);
