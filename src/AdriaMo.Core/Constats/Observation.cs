namespace AdriaMo.Core.Constats;

/// <summary>When a defect was seen.</summary>
public static class Observation
{
    /// <summary>
    /// The instant a piece shows: when its photo was taken (a constat made on Monday and added on Thursday is dated Monday), or
    /// when it was added if the photo has no date or a date after its own upload, which only a wrong camera clock gives.
    /// </summary>
    /// <param name="takenAt">The date of the photo, if any.</param>
    /// <param name="addedAt">When the piece was added.</param>
    /// <returns>The instant of the observation.</returns>
    public static DateTimeOffset Of(DateTimeOffset? takenAt, DateTimeOffset addedAt) =>
        takenAt is { } taken && taken <= addedAt ? taken : addedAt;
}
