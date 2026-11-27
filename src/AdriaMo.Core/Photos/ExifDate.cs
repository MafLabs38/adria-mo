using System.Globalization;

namespace AdriaMo.Core.Photos;

/// <summary>The date a photo was taken, read from its EXIF fields.</summary>
public static class ExifDate
{
    /// <summary>
    /// Reads <c>DateTimeOriginal</c> ("2026:11:27 14:05:00") and, when the camera wrote it, <c>OffsetTimeOriginal</c> ("+01:00").
    /// Without an offset the time is the building's local time: a phone sets its clock to the time zone it is in.
    /// </summary>
    /// <param name="dateTimeOriginal">The EXIF date and time, or <c>null</c>.</param>
    /// <param name="offsetTimeOriginal">The EXIF offset, or <c>null</c>.</param>
    /// <param name="buildingTimeZone">The time zone of the building.</param>
    /// <returns>The instant, or <c>null</c> when the photo has no readable date.</returns>
    public static DateTimeOffset? Parse(string? dateTimeOriginal, string? offsetTimeOriginal, TimeZoneInfo buildingTimeZone)
    {
        ArgumentNullException.ThrowIfNull(buildingTimeZone);
        if (!DateTime.TryParseExact(
                dateTimeOriginal?.Trim().TrimEnd('\0'),
                "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }

        var offset = ParseOffset(offsetTimeOriginal) ?? buildingTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static TimeSpan? ParseOffset(string? value)
    {
        var text = value?.Trim().TrimEnd('\0');
        if (string.IsNullOrEmpty(text) || (text[0] != '+' && text[0] != '-'))
        {
            return null;
        }

        if (!TimeSpan.TryParseExact(text[1..], @"hh\:mm", CultureInfo.InvariantCulture, out var span) || span > TimeSpan.FromHours(14))
        {
            return null;
        }

        return text[0] == '-' ? span.Negate() : span;
    }
}
