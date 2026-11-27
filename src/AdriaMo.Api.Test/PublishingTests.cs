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
        File.Exists(Path.Combine(factory.PublishFolder, photo.GetProperty("src").GetString()!)).Should().BeTrue();
        File.Exists(Path.Combine(factory.PublishFolder, photo.GetProperty("thumbnail").GetString()!)).Should().BeTrue();
        snapshot.RootElement.GetProperty("timeZone").GetString().Should().Be("Europe/Paris");

        var text = await File.ReadAllTextAsync(Path.Combine(factory.PublishFolder, SnapshotPublisher.SnapshotFile), Ct);
        text.Should().NotContain("publie").And.NotContain("author", "no account is published")
            .And.NotContain("sha256", "nor the proof of the original").And.NotContain("original");
        Directory.GetFiles(Path.Combine(factory.PublishFolder, "photos")).Should().HaveCount(
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
        File.Exists(Path.Combine(factory.PublishFolder, photo)).Should().BeFalse();
    }

    [Fact]
    public async Task A_change_is_published_by_the_background_publisher()
    {
        var admin = await factory.AdminAsync("arriere-plan");
        var number = await ConstatApiTests.CreateAsync(admin);
        var file = Path.Combine(factory.PublishFolder, SnapshotPublisher.SnapshotFile);

        // The factory gathers changes for no time: the snapshot follows within moments.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && !(File.Exists(file) && (await File.ReadAllTextAsync(file, Ct)).Contains($"\"number\":{number},", StringComparison.Ordinal)))
        {
            await Task.Delay(50, Ct);
        }

        (await File.ReadAllTextAsync(file, Ct)).Should().Contain($"\"number\":{number},");
    }

    private async Task<JsonDocument> PublishAsync()
    {
        await factory.Services.GetRequiredService<SnapshotPublisher>().PublishAsync(Ct);
        return JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(factory.PublishFolder, SnapshotPublisher.SnapshotFile), Ct));
    }
}
