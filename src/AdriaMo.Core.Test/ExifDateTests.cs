using AdriaMo.Core.Photos;

namespace AdriaMo.Core.Test;

public sealed class ExifDateTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    [Fact]
    public void The_offset_written_by_the_camera_wins() =>
        ExifDate.Parse("2026:07:14 10:30:00", "-04:00", Paris)
            .Should().Be(new DateTimeOffset(2026, 7, 14, 10, 30, 0, TimeSpan.FromHours(-4)));

    [Fact]
    public void Without_an_offset_a_winter_time_is_paris_winter_time() =>
        ExifDate.Parse("2026:01:10 08:00:00", null, Paris)
            .Should().Be(new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(1)));

    [Fact]
    public void Without_an_offset_a_summer_time_is_paris_summer_time() =>
        ExifDate.Parse("2026:07:10 08:00:00", "", Paris)
            .Should().Be(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.FromHours(2)));

    [Fact]
    public void Padding_and_trailing_nul_characters_are_ignored() =>
        ExifDate.Parse(" 2026:01:10 08:00:00\0", "+01:00\0", Paris)
            .Should().Be(new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("2026-01-10 08:00:00")]
    [InlineData("2026:13:10 08:00:00")]
    public void An_absent_or_unreadable_date_gives_no_date(string? value) =>
        ExifDate.Parse(value, null, Paris).Should().BeNull();

    [Theory]
    [InlineData("01:00")]
    [InlineData("+1:00")]
    [InlineData("+15:00")]
    [InlineData("+ab:cd")]
    public void An_unreadable_offset_falls_back_to_the_building_time_zone(string offset) =>
        ExifDate.Parse("2026:01:10 08:00:00", offset, Paris)
            .Should().Be(new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(1)));

    [Fact]
    public void The_largest_offset_is_accepted() =>
        ExifDate.Parse("2026:01:10 08:00:00", "+14:00", Paris)!.Value.Offset.Should().Be(TimeSpan.FromHours(14));

    [Fact]
    public void A_time_zone_is_required()
    {
        var parse = () => ExifDate.Parse("2026:01:10 08:00:00", null, null!);
        parse.Should().Throw<ArgumentNullException>();
    }
}
