namespace AdriaMo.Core.Photos;

/// <summary>
/// Photos taken a few minutes apart, most likely of the same defect (spec §4.1): the inbox offers to attach a whole series to one
/// constat at once.
/// </summary>
/// <param name="Photos">The photos of the series, oldest first.</param>
public sealed record PhotoSeries(IReadOnlyList<TakenPhoto> Photos)
{
    /// <summary>When the first photo of the series was taken.</summary>
    public DateTimeOffset StartedAt => Photos[0].TakenAt;

    /// <summary>When the last photo of the series was taken.</summary>
    public DateTimeOffset EndedAt => Photos[^1].TakenAt;
}

/// <summary>A photo and the moment it was taken (read from its EXIF data, not the moment it was uploaded).</summary>
/// <param name="Id">The identifier of the photo.</param>
/// <param name="TakenAt">When the photo was taken.</param>
public sealed record TakenPhoto(string Id, DateTimeOffset TakenAt);
