using System.Net.Http.Json;
using System.Text.Json;
using AdriaMo.Api.Publishing;
using Microsoft.Extensions.DependencyInjection;

namespace AdriaMo.Api.Test;

[Trait("Category", "Integration")]
public sealed class PublishingTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_public_constats_and_their_reduced_photos_are_published()
    {
        var admin = await factory.AdminAsync("publie");
        var shown = await ConstatApiTests.CreateAsync(admin);
        var hidden = await ConstatApiTests.CreateAsync(admin, isPublic: false);
        await ConstatApiTests.PostPieceAsync(admin, shown, TestPhotos.Jpeg(takenAt: "2026:11:23 09:15:00", gps: true, colour: new SkiaSharp.SKColor(10, 200, 10)), "Gond arraché");
        await ConstatApiTests.PostPieceAsync(admin, hidden, TestPhotos.Jpeg(colour: new SkiaSharp.SKColor(10, 10, 200)));

        var snapshot = await PublishAsync();

        var numbers = snapshot.RootElement.GetProperty("constats").EnumerateArray().Select(c => c.GetProperty("number").GetInt32()).ToArray();
        numbers.Should().Contain(shown).And.NotContain(hidden);
        var constat = snapshot.RootElement.GetProperty("constats").EnumerateArray().Single(c => c.GetProperty("number").GetInt32() == shown);
        constat.GetProperty("category").GetString().Should().Be("Portes et fermetures");
        constat.GetProperty("firstObservedAt").GetString().Should().Be("2026-11-23T09:15:00+01:00");
        var photo = constat.GetProperty("pieces")[0].GetProperty("photo");
        File.Exists(Path.Join(factory.PublishFolder, photo.GetProperty("src").GetString()!)).Should().BeTrue();
        File.Exists(Path.Join(factory.PublishFolder, photo.GetProperty("thumbnail").GetString()!)).Should().BeTrue();
        snapshot.RootElement.GetProperty("timeZone").GetString().Should().Be("Europe/Paris");

        var text = await File.ReadAllTextAsync(Path.Join(factory.PublishFolder, SnapshotPublisher.SnapshotFile), Ct);
        text.Should().NotContain("publie").And.NotContain("author", "no account is published")
            .And.NotContain("sha256", "nor the proof of the original").And.NotContain("original");
        Directory.GetFiles(Path.Join(factory.PublishFolder, "photos")).Should().HaveCount(
            snapshot.RootElement.GetProperty("constats").EnumerateArray().SelectMany(c => c.GetProperty("pieces").EnumerateArray())
                .Count(p => p.GetProperty("photo").ValueKind == JsonValueKind.Object) * 2);
    }

    [Fact]
    public async Task A_constat_made_private_leaves_the_snapshot_with_its_photos()
    {
        var admin = await factory.AdminAsync("retire");
        var number = await ConstatApiTests.CreateAsync(admin);
        await ConstatApiTests.PostPieceAsync(admin, number, TestPhotos.Jpeg(colour: new SkiaSharp.SKColor(250, 250, 0)));
        var before = await PublishAsync();
        var photo = before.RootElement.GetProperty("constats").EnumerateArray().Single(c => c.GetProperty("number").GetInt32() == number)
            .GetProperty("pieces")[0].GetProperty("photo").GetProperty("src").GetString()!;

        await admin.PutAsJsonAsync($"/api/v1/constats/{number}", new { title = "Porte du hall forcée", category = "portes", isPublic = false }, Ct);
        var after = await PublishAsync();

        after.RootElement.GetProperty("constats").EnumerateArray().Should().NotContain(c => c.GetProperty("number").GetInt32() == number);
        File.Exists(Path.Join(factory.PublishFolder, photo)).Should().BeFalse();
    }

    [Fact]
    public async Task A_photo_whose_files_are_missing_is_left_out_and_the_rest_is_published()
    {
        var admin = await factory.AdminAsync("fichier-perdu");
        var number = await ConstatApiTests.CreateAsync(admin);
        var piece = await (await ConstatApiTests.PostPieceAsync(admin, number, TestPhotos.Jpeg(colour: new SkiaSharp.SKColor(3, 140, 3)), "Gond arraché"))
            .Content.ReadFromJsonAsync<Constats.PieceDetail>(Ct);
        await PublishAsync();
        File.Delete(Path.Join(factory.PublishFolder, "photos", piece!.Photo!));
        File.Delete(Path.Join(factory.DataFolder, "photos", piece.Photo!));

        var snapshot = await PublishAsync();

        var published = snapshot.RootElement.GetProperty("constats").EnumerateArray().Single(c => c.GetProperty("number").GetInt32() == number);
        published.GetProperty("pieces")[0].GetProperty("text").GetString().Should().Be("Gond arraché");
        published.GetProperty("pieces")[0].GetProperty("photo").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_administration_sees_when_the_public_page_was_last_published()
    {
        var admin = await factory.AdminAsync("etat-publication");
        await PublishAsync();

        var state = await admin.GetFromJsonAsync<PublicationState>("/api/v1/publication", Ct);

        state!.LastSuccessAt.Should().NotBeNull();
        state.LastAttemptAt.Should().Be(state.LastSuccessAt);
        state.LastError.Should().BeNull();
    }

    private async Task<JsonDocument> PublishAsync()
    {
        await factory.Services.GetRequiredService<SnapshotPublisher>().PublishAsync(Ct);
        return JsonDocument.Parse(await File.ReadAllTextAsync(Path.Join(factory.PublishFolder, SnapshotPublisher.SnapshotFile), Ct));
    }
}
