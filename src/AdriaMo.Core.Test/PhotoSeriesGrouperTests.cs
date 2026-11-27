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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_gap_must_be_positive(int minutes)
    {
        var create = () => new PhotoSeriesGrouper(TimeSpan.FromMinutes(minutes));

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
