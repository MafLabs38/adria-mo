using AdriaMo.Core.Constats;

namespace AdriaMo.Core.Test;

public sealed class ConstatTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset Thursday = Monday.AddDays(3);

    [Fact]
    public void A_photo_taken_on_monday_and_added_on_thursday_was_seen_on_monday() =>
        Observation.Of(Monday, Thursday).Should().Be(Monday);

    [Fact]
    public void An_undated_piece_was_seen_when_it_was_added() => Observation.Of(null, Thursday).Should().Be(Thursday);

    [Fact]
    public void A_photo_dated_after_its_upload_has_a_wrong_clock_and_counts_as_added() =>
        Observation.Of(Thursday.AddDays(1), Thursday).Should().Be(Thursday);

    [Fact]
    public void A_photo_taken_the_instant_it_was_added_keeps_its_date() =>
        Observation.Of(Thursday, Thursday).Should().Be(Thursday);

    [Fact]
    public void A_constat_without_piece_was_first_seen_when_created() =>
        NewConstat().FirstObservedAt.Should().Be(Thursday);

    [Fact]
    public void A_constat_was_first_seen_at_its_oldest_observation_not_its_first_upload()
    {
        var constat = NewConstat();
        constat.Pieces.Add(NewPiece(takenAt: Monday.AddDays(1), addedAt: Thursday));
        constat.Pieces.Add(NewPiece(takenAt: Monday, addedAt: Thursday.AddDays(1)));

        constat.FirstObservedAt.Should().Be(Monday);
        constat.Pieces.First().ObservedAt.Should().Be(Monday.AddDays(1));
    }

    [Fact]
    public void A_new_constat_is_open_and_public()
    {
        var constat = NewConstat();
        constat.Status.Should().Be(ConstatStatus.Open);
        constat.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Categories_have_unique_lower_case_keys_and_a_label()
    {
        Categories.All.Select(c => c.Key).Should().OnlyHaveUniqueItems().And.AllSatisfy(k => k.Should().Be(k.ToLowerInvariant()));
        Categories.All.Should().AllSatisfy(c => c.Label.Should().NotBeNullOrWhiteSpace());
        Categories.All.Should().HaveCount(13);
    }

    [Fact]
    public void A_category_is_found_by_its_exact_key()
    {
        Categories.Find("portes")!.Label.Should().Be("Portes et fermetures");
        Categories.Find("Portes").Should().BeNull();
        Categories.Find(null).Should().BeNull();
    }

    internal static Constat NewConstat(int id = 1, bool isPublic = true) => new()
    {
        Id = id,
        Title = "Porte du hall forcée",
        Category = "portes",
        CreatedAt = Thursday,
        UpdatedAt = Thursday,
        IsPublic = isPublic,
    };

    internal static Piece NewPiece(DateTimeOffset? takenAt, DateTimeOffset addedAt, int id = 0, PiecePhoto? photo = null, string? text = null) => new()
    {
        Id = id,
        TakenAt = takenAt,
        AddedAt = addedAt,
        AuthorId = "author-1",
        Photo = photo,
        Text = text,
    };
}
