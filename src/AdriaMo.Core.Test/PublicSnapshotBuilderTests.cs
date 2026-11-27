using AdriaMo.Core.Constats;
using AdriaMo.Core.Publishing;

namespace AdriaMo.Core.Test;

public sealed class PublicSnapshotBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 14, 5, 0, TimeSpan.FromHours(1));

    private static readonly PiecePhoto Photo = new("0a1b", "0a1b.jpg", "pub1.jpg", "thumb1.jpg", 1600, 1200);

    [Fact]
    public void Only_public_constats_are_published()
    {
        var snapshot = PublicSnapshotBuilder.Build(
            [ConstatTests.NewConstat(1), ConstatTests.NewConstat(2, isPublic: false)], Now, "Europe/Paris");

        snapshot.Constats.Select(c => c.Number).Should().Equal(1);
        snapshot.Version.Should().Be(1);
        snapshot.PublishedAt.Should().Be(Now);
        snapshot.TimeZone.Should().Be("Europe/Paris");
    }

    [Fact]
    public void The_most_recently_changed_constat_comes_first_then_the_highest_number()
    {
        var older = ConstatTests.NewConstat(1);
        var newer = ConstatTests.NewConstat(2);
        var sameAsNewer = ConstatTests.NewConstat(3);
        older.UpdatedAt = Now.AddDays(-2);
        newer.UpdatedAt = Now;
        sameAsNewer.UpdatedAt = Now;

        PublicSnapshotBuilder.Build([older, newer, sameAsNewer], Now, "Europe/Paris")
            .Constats.Select(c => c.Number).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void A_constat_shows_its_label_status_first_observation_and_pieces_oldest_first()
    {
        var constat = ConstatTests.NewConstat(7);
        constat.Location = "Hall bât. B";
        constat.Status = ConstatStatus.ReportedToLandlord;
        constat.Pieces.Add(ConstatTests.NewPiece(Now.AddDays(-1), Now, id: 2, text: "Toujours cassée"));
        constat.Pieces.Add(ConstatTests.NewPiece(Now.AddDays(-5), Now, id: 1, photo: Photo));

        var published = PublicSnapshotBuilder.Build([constat], Now, "Europe/Paris").Constats.Single();

        published.Title.Should().Be("Porte du hall forcée");
        published.Category.Should().Be("Portes et fermetures");
        published.Location.Should().Be("Hall bât. B");
        published.Status.Should().Be("reported-to-landlord");
        published.FirstObservedAt.Should().Be(Now.AddDays(-5));
        published.UpdatedAt.Should().Be(constat.UpdatedAt);
        published.Pieces.Should().HaveCount(2);
        published.Pieces[0].Photo.Should().Be(new PublicPhoto("photos/pub1.jpg", "photos/thumb1.jpg", 1600, 1200));
        published.Pieces[0].Text.Should().BeNull();
        published.Pieces[1].Text.Should().Be("Toujours cassée");
        published.Pieces[1].Photo.Should().BeNull();
    }

    [Fact]
    public void Pieces_seen_at_the_same_instant_keep_the_order_they_were_added()
    {
        var constat = ConstatTests.NewConstat();
        constat.Pieces.Add(ConstatTests.NewPiece(Now, Now, id: 9, text: "second"));
        constat.Pieces.Add(ConstatTests.NewPiece(Now, Now, id: 4, text: "first"));

        PublicSnapshotBuilder.Build([constat], Now, "Europe/Paris").Constats.Single()
            .Pieces.Select(p => p.Text).Should().Equal("first", "second");
    }

    [Fact]
    public void An_open_constat_has_the_status_open() =>
        PublicSnapshotBuilder.Build([ConstatTests.NewConstat()], Now, "Europe/Paris").Constats.Single().Status.Should().Be("open");

    [Fact]
    public void An_unknown_category_key_is_shown_as_it_is()
    {
        var constat = ConstatTests.NewConstat();
        constat.Category = "ancienne";

        PublicSnapshotBuilder.Build([constat], Now, "Europe/Paris").Constats.Single().Category.Should().Be("ancienne");
    }

    [Fact]
    public void The_published_files_are_the_photos_of_public_constats_only()
    {
        var shown = ConstatTests.NewConstat(1);
        shown.Pieces.Add(ConstatTests.NewPiece(Now, Now, photo: Photo));
        shown.Pieces.Add(ConstatTests.NewPiece(Now, Now, text: "sans photo"));
        var hidden = ConstatTests.NewConstat(2, isPublic: false);
        hidden.Pieces.Add(ConstatTests.NewPiece(Now, Now, photo: Photo with { PublishedFileName = "secret.jpg", ThumbnailFileName = "secret-t.jpg" }));

        PublicSnapshotBuilder.PublishedFiles([shown, hidden]).Should().BeEquivalentTo(["pub1.jpg", "thumb1.jpg"]);
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var noConstats = () => PublicSnapshotBuilder.Build(null!, Now, "Europe/Paris");
        var noZone = () => PublicSnapshotBuilder.Build([], Now, " ");
        var noFiles = () => PublicSnapshotBuilder.PublishedFiles(null!);

        noConstats.Should().Throw<ArgumentNullException>();
        noZone.Should().Throw<ArgumentException>();
        noFiles.Should().Throw<ArgumentNullException>();
    }
}
