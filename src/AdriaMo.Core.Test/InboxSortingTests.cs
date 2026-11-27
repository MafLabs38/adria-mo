using AdriaMo.Core.Constats;
using AdriaMo.Core.Photos;

namespace AdriaMo.Core.Test;

public sealed class InboxSortingTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset Thursday = Monday.AddDays(3);

    private static InboxPhoto Photo(int id, DateTimeOffset? takenAt, DateTimeOffset? addedAt = null) => new()
    {
        Id = id,
        Photo = new PiecePhoto($"{id:x64}", $"{id:x64}.jpg", "published.jpg", "thumbnail.jpg", 1600, 1200),
        TakenAt = takenAt,
        AddedAt = addedAt ?? Thursday,
        AuthorId = "eric",
    };

    [Fact]
    public void Photos_taken_minutes_apart_form_a_series_and_the_most_recent_series_comes_first()
    {
        var sorted = InboxSorting.Of(
            [Photo(1, Monday.AddMinutes(2)), Photo(2, Monday.AddHours(3)), Photo(3, Monday), Photo(4, Monday.AddHours(3).AddMinutes(1))],
            new PhotoSeriesGrouper());

        sorted.Series.Should().HaveCount(2);
        sorted.Series[0].Select(p => p.Id).Should().Equal(2, 4);
        sorted.Series[1].Select(p => p.Id).Should().Equal(3, 1);
        sorted.Undated.Should().BeEmpty();
    }

    [Fact]
    public void A_photo_without_a_date_or_dated_after_its_upload_waits_apart_the_last_sent_first()
    {
        var undated = Photo(1, null, Thursday.AddMinutes(-5));
        var wrongClock = Photo(2, Thursday.AddDays(1), Thursday);
        var dated = Photo(3, Monday);

        var sorted = InboxSorting.Of([undated, wrongClock, dated], new PhotoSeriesGrouper());

        sorted.Undated.Select(p => p.Id).Should().Equal(2, 1);
        sorted.Series.Should().ContainSingle().Which.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void A_photo_taken_when_it_was_sent_is_dated_and_grouped()
    {
        var sorted = InboxSorting.Of([Photo(1, Thursday, Thursday)], new PhotoSeriesGrouper());

        sorted.Series.Should().ContainSingle();
        sorted.Undated.Should().BeEmpty();
    }

    [Fact]
    public void An_empty_inbox_sorts_to_nothing()
    {
        var sorted = InboxSorting.Of([], new PhotoSeriesGrouper());

        sorted.Series.Should().BeEmpty();
        sorted.Undated.Should().BeEmpty();
    }

    [Fact]
    public void A_photo_is_dated_when_taken_before_or_when_it_was_sent()
    {
        Photo(1, Thursday).IsDated.Should().BeTrue();
        Photo(1, Thursday.AddTicks(1)).IsDated.Should().BeFalse();
        Photo(1, null).IsDated.Should().BeFalse();
    }

    [Fact]
    public void A_photo_becomes_a_piece_with_its_files_dates_and_author()
    {
        var photo = Photo(7, Monday);

        var piece = photo.ToPiece(42, "Gond arraché");

        piece.ConstatId.Should().Be(42);
        piece.Text.Should().Be("Gond arraché");
        piece.Photo.Should().Be(photo.Photo).And.NotBeSameAs(photo.Photo);
        piece.TakenAt.Should().Be(Monday);
        piece.AddedAt.Should().Be(Thursday, "the deposit is when the photo reached the back end, not when it was classified");
        piece.AuthorId.Should().Be("eric");
        piece.ObservedAt.Should().Be(Monday);
        photo.ObservedAt.Should().Be(Monday);
        Photo(8, null).ObservedAt.Should().Be(Thursday, "an undated photo is attached in the order it was sent");
    }
}
