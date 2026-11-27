namespace AdriaMo.Core.Photos;

/// <summary>Splits photos into series: a new series starts when more than <see cref="MaxGap"/> separates two photos in time.</summary>
public sealed class PhotoSeriesGrouper
{
    /// <summary>The gap used when none is given: a few minutes, the time to walk from one landing to the next.</summary>
    public static readonly TimeSpan DefaultMaxGap = TimeSpan.FromMinutes(5);

    /// <summary>Creates a grouper.</summary>
    /// <param name="maxGap">The longest time between two photos of the same series; positive.</param>
    public PhotoSeriesGrouper(TimeSpan maxGap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxGap, TimeSpan.Zero);
        MaxGap = maxGap;
    }

    /// <summary>Creates a grouper with <see cref="DefaultMaxGap"/>.</summary>
    public PhotoSeriesGrouper()
        : this(DefaultMaxGap)
    {
    }

    /// <summary>The longest time between two photos of the same series.</summary>
    public TimeSpan MaxGap { get; }

    /// <summary>Groups photos into series, oldest first, whatever the order they are given in.</summary>
    /// <param name="photos">The photos to group.</param>
    /// <returns>The series, oldest first; empty when there is no photo.</returns>
    public IReadOnlyList<PhotoSeries> Group(IEnumerable<TakenPhoto> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);

        var series = new List<PhotoSeries>();
        var current = new List<TakenPhoto>();
        foreach (var photo in photos.OrderBy(p => p.TakenAt).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            if (current.Count > 0 && photo.TakenAt - current[^1].TakenAt > MaxGap)
            {
                series.Add(new PhotoSeries(current));
                current = [];
            }

            current.Add(photo);
        }

        if (current.Count > 0)
        {
            series.Add(new PhotoSeries(current));
        }

        return series;
    }
}
