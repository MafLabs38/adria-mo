using AdriaMo.Core.Photos;

namespace AdriaMo.Core.Test;

public sealed class PhotoSeriesGrouperTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(1));

    private static TakenPhoto At(string id, int minutes) => new(id, Monday.AddMinutes(minutes));

    [Fact]
    public void No_photo_gives_no_series()
    {
        new PhotoSeriesGrouper().Group([]).Should().BeEmpty();
    }

    [Fact]
    public void Photos_a_few_minutes_apart_form_one_series()
    {
        var series = new PhotoSeriesGrouper().Group([At("a", 0), At("b", 2), At("c", 6)]);

        series.Should().ContainSingle().Which.Photos.Select(p => p.Id).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void A_gap_longer_than_the_limit_starts_a_new_series()
    {
        var series = new PhotoSeriesGrouper(TimeSpan.FromMinutes(5)).Group([At("hall", 0), At("hall-2", 1), At("landing", 40)]);

        series.Select(s => s.Photos.Count).Should().Equal(2, 1);
        series[1].StartedAt.Should().Be(Monday.AddMinutes(40));
    }

    [Fact]
    public void A_gap_equal_to_the_limit_stays_in_the_same_series()
    {
        new PhotoSeriesGrouper(TimeSpan.FromMinutes(5)).Group([At("a", 0), At("b", 5)]).Should().ContainSingle();
    }

    [Fact]
    public void Photos_are_ordered_by_the_time_they_were_taken_not_the_order_given()
    {
        var series = new PhotoSeriesGrouper().Group([At("late", 3), At("early", 0), At("middle", 1)]);

        var only = series.Should().ContainSingle().Subject;
        only.Photos.Select(p => p.Id).Should().Equal("early", "middle", "late");
        only.StartedAt.Should().Be(Monday);
        only.EndedAt.Should().Be(Monday.AddMinutes(3));
    }

    [Fact]
    public void Photos_taken_at_the_same_moment_keep_a_stable_order()
    {
        var series = new PhotoSeriesGrouper().Group([At("b", 0), At("a", 0)]);

        series[0].Photos.Select(p => p.Id).Should().Equal("a", "b");
    }

    [Fact]
    public void A_single_photo_is_a_series_that_starts_and_ends_with_it()
    {
        var only = new PhotoSeriesGrouper().Group([At("a", 0)]).Should().ContainSingle().Subject;

        only.StartedAt.Should().Be(only.EndedAt);
    }

    [Fact]
    public void A_long_chain_of_close_photos_stays_one_series_even_when_it_lasts_longer_than_the_gap()
    {
        var series = new PhotoSeriesGrouper(TimeSpan.FromMinutes(5)).Group([At("a", 0), At("b", 4), At("c", 8), At("d", 12)]);

        series.Should().ContainSingle().Which.Photos.Should().HaveCount(4);
    }

    [Fact]
    public void Photos_are_compared_as_instants_whatever_the_offset_of_the_camera()
    {
        var paris = new TakenPhoto("paris", new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.FromHours(1)));
        var utc = new TakenPhoto("utc", new DateTimeOffset(2026, 11, 23, 9, 3, 0, TimeSpan.Zero));

        new PhotoSeriesGrouper().Group([paris, utc]).Should().ContainSingle().Which.Photos.Select(p => p.Id).Should().Equal("paris", "utc");
    }

    [Fact]
    public void No_list_is_refused()
    {
        var group = () => new PhotoSeriesGrouper().Group(null!);

        group.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void A_series_cannot_be_empty()
    {
        var create = () => new PhotoSeries([]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_series_cannot_be_out_of_order()
    {
        var create = () => new PhotoSeries([At("late", 5), At("early", 0)]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_series_keeps_its_own_copy_of_the_photos()
    {
        var photos = new List<TakenPhoto> { At("a", 0) };
        var series = new PhotoSeries(photos);

        photos.Add(At("b", 1));

        series.Photos.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_gap_must_be_positive(int minutes)
    {
        var create = () => new PhotoSeriesGrouper(TimeSpan.FromMinutes(minutes));

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
