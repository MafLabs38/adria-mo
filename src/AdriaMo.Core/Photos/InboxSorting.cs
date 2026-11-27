using System.Globalization;

namespace AdriaMo.Core.Photos;

/// <summary>The inbox as it is shown (spec §4.1): the dated photos in series, the most recent series first, and the undated ones.</summary>
/// <param name="Series">The series of dated photos, the most recent first; the photos of a series oldest first.</param>
/// <param name="Undated">The photos without a usable date, the most recently sent first.</param>
public sealed record InboxSorting(IReadOnlyList<IReadOnlyList<InboxPhoto>> Series, IReadOnlyList<InboxPhoto> Undated)
{
    /// <summary>Sorts the inbox by the date the photos were taken, and groups photos taken a few minutes apart.</summary>
    /// <param name="photos">The photos of the inbox.</param>
    /// <param name="grouper">How photos are grouped.</param>
    /// <returns>The sorted inbox.</returns>
    public static InboxSorting Of(IEnumerable<InboxPhoto> photos, PhotoSeriesGrouper grouper)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(grouper);
        var all = photos.ToArray();
        var byKey = all.ToDictionary(Key);
        var dated = all.Where(p => p.IsDated).Select(p => new TakenPhoto(Key(p), p.TakenAt!.Value));
        var series = grouper.Group(dated)
            .Reverse()
            .Select(s => (IReadOnlyList<InboxPhoto>)[.. s.Photos.Select(p => byKey[p.Id])])
            .ToArray();
        var undated = all.Where(p => !p.IsDated).OrderByDescending(p => p.AddedAt).ThenByDescending(p => p.Id).ToArray();
        return new InboxSorting(series, undated);
    }

    private static string Key(InboxPhoto photo) => photo.Id.ToString(CultureInfo.InvariantCulture);
}
