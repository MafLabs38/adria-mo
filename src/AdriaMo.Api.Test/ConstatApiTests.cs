using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdriaMo.Api.Constats;
using AdriaMo.Core.Constats;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;

namespace AdriaMo.Api.Test;

[Trait("Category", "Integration")]
public sealed class ConstatApiTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_administrator_creates_a_constat_that_is_open_and_public()
    {
        var admin = await factory.AdminAsync("cree");

        var response = await admin.PostAsJsonAsync("/api/v1/constats", new { title = "  Porte du hall forcée ", category = "portes", location = "Hall B" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ConstatDetail>(Ct);
        created!.Title.Should().Be("Porte du hall forcée");
        created.Status.Should().Be("open");
        created.IsPublic.Should().BeTrue();
        created.Pieces.Should().BeEmpty();
        response.Headers.Location!.ToString().Should().Be($"/api/v1/constats/{created.Number}");
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("\"status\":\"open\"");
        var list = await admin.GetFromJsonAsync<ConstatSummary[]>("/api/v1/constats", Ct);
        list.Should().Contain(c => c.Number == created.Number && c.PieceCount == 0 && c.Thumbnail == null);
    }

    [Fact]
    public async Task A_constat_needs_a_title_and_a_known_category()
    {
        var admin = await factory.AdminAsync("valide");

        var response = await admin.PostAsJsonAsync("/api/v1/constats", new { title = " ", category = "inconnue", location = new string('x', 121) }, Ct);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await AccountTests.Codes(response, "title")).Should().Equal("Required");
        (await AccountTests.Codes(response, "category")).Should().Equal("Unknown");
        (await AccountTests.Codes(response, "location")).Should().Equal("TooLong");
        var tooLong = await admin.PostAsJsonAsync("/api/v1/constats", new { title = new string('x', 121), category = "portes" }, Ct);
        (await AccountTests.Codes(tooLong, "title")).Should().Equal("TooLong");
    }

    [Fact]
    public async Task A_constat_is_updated_and_an_unknown_one_is_not_found()
    {
        var admin = await factory.AdminAsync("modifie");
        var number = await CreateAsync(admin);

        var updated = await admin.PutAsJsonAsync($"/api/v1/constats/{number}", new { title = "Ampoule grillée", category = "eclairage", location = " ", isPublic = false }, Ct);

        var detail = await updated.Content.ReadFromJsonAsync<ConstatDetail>(Ct);
        detail!.Title.Should().Be("Ampoule grillée");
        detail.Category.Should().Be("eclairage");
        detail.Location.Should().BeNull();
        detail.IsPublic.Should().BeFalse();
        (await admin.GetAsync("/api/v1/constats/99999", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync("/api/v1/constats/99999", new { title = "x", category = "portes" }, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync($"/api/v1/constats/{number}", new { title = "", category = "portes" }, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetFromJsonAsync<Category[]>("/api/v1/categories", Ct)).Should().HaveCount(13);
    }

    [Fact]
    public async Task A_photo_is_dated_by_its_exif_and_its_published_copy_carries_no_metadata()
    {
        var admin = await factory.AdminAsync("photographe");
        var number = await CreateAsync(admin);
        var original = TestPhotos.Jpeg(width: 3200, height: 2400, takenAt: "2026:11:23 09:15:00", gps: true);

        var response = await PostPieceAsync(admin, number, original, "Gond arraché");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var piece = await response.Content.ReadFromJsonAsync<PieceDetail>(Ct);
        piece!.TakenAt.Should().Be(new DateTimeOffset(2026, 11, 23, 9, 15, 0, TimeSpan.FromHours(1)));
        piece.ObservedAt.Should().Be(piece.TakenAt!.Value);
        piece.Text.Should().Be("Gond arraché");
        piece.OriginalSha256.Should().Be(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(original)));

        var keptOriginal = await File.ReadAllBytesAsync(Path.Join(factory.DataFolder, "originals", piece.OriginalSha256 + ".jpg"), Ct);
        keptOriginal.Should().Equal(original);

        var published = await admin.GetByteArrayAsync($"/api/v1/photos/{piece.Photo}", Ct);
        using var publishedStream = new MemoryStream(published);
        var metadata = ImageMetadataReader.ReadMetadata(publishedStream);
        metadata.OfType<ExifSubIfdDirectory>().Should().BeEmpty();
        metadata.OfType<GpsDirectory>().Should().BeEmpty();
        var size = metadata.OfType<JpegDirectory>().Single();
        (size.GetImageWidth(), size.GetImageHeight()).Should().Be((1600, 1200));
        using var thumbnailStream = new MemoryStream(await admin.GetByteArrayAsync($"/api/v1/photos/{piece.Thumbnail}", Ct));
        var thumbnail = ImageMetadataReader.ReadMetadata(thumbnailStream).OfType<JpegDirectory>().Single();
        (thumbnail.GetImageWidth(), thumbnail.GetImageHeight()).Should().Be((480, 360));

        var detail = await admin.GetFromJsonAsync<ConstatDetail>($"/api/v1/constats/{number}", Ct);
        detail!.FirstObservedAt.Should().Be(piece.TakenAt!.Value);
        var summary = (await admin.GetFromJsonAsync<ConstatSummary[]>("/api/v1/constats", Ct))!.Single(c => c.Number == number);
        summary.Thumbnail.Should().Be(piece.Thumbnail);
        summary.PieceCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 64, 40, Corner.TopLeft)]
    [InlineData(2, 64, 40, Corner.TopRight)]
    [InlineData(3, 64, 40, Corner.BottomRight)]
    [InlineData(4, 64, 40, Corner.BottomLeft)]
    [InlineData(5, 40, 64, Corner.TopLeft)]
    [InlineData(6, 40, 64, Corner.TopRight)]
    [InlineData(7, 40, 64, Corner.BottomRight)]
    [InlineData(8, 40, 64, Corner.BottomLeft)]
    public async Task A_photo_taken_with_the_phone_turned_is_published_upright(ushort orientation, int width, int height, Corner red)
    {
        // The stored pixels have red in their top-left corner; the EXIF orientation says how to turn them to show the scene.
        var admin = await AdminAsync();
        var number = await CreateAsync(admin);
        var photo = TestPhotos.Jpeg(64, 40, orientation: orientation, markTopLeft: true);

        var piece = await (await PostPieceAsync(admin, number, photo)).Content.ReadFromJsonAsync<PieceDetail>(Ct);

        using var published = SkiaSharp.SKBitmap.Decode(await admin.GetByteArrayAsync($"/api/v1/photos/{piece!.Photo}", Ct));
        (published.Width, published.Height).Should().Be((width, height));
        RedCorner(published).Should().Be(red);
    }

    public enum Corner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft,
    }

    private static Corner RedCorner(SkiaSharp.SKBitmap bitmap)
    {
        bool IsRed(int x, int y) => bitmap.GetPixel(x, y) is { Red: > 200, Blue: < 60 };
        var (left, right, top, bottom) = (bitmap.Width / 4, bitmap.Width * 3 / 4, bitmap.Height / 4, bitmap.Height * 3 / 4);
        return new[] { Corner.TopLeft, Corner.TopRight, Corner.BottomRight, Corner.BottomLeft }.Single(c => c switch
        {
            Corner.TopLeft => IsRed(left, top),
            Corner.TopRight => IsRed(right, top),
            Corner.BottomRight => IsRed(right, bottom),
            _ => IsRed(left, bottom),
        });
    }

    [Fact]
    public async Task A_photo_claiming_more_than_64_million_pixels_is_refused_before_decoding()
    {
        var admin = await AdminAsync();
        var number = await CreateAsync(admin);

        var response = await PostPieceAsync(admin, number, TestPhotos.PngHeader(10_000, 10_000));

        (await AccountTests.Codes(response, "photo")).Should().Equal("TooManyPixels");
    }

    [Fact]
    public async Task A_file_over_25_megabytes_is_refused()
    {
        var admin = await AdminAsync();
        var number = await CreateAsync(admin);

        var response = await PostPieceAsync(admin, number, new byte[Photos.PhotoIntake.MaxBytes + 1]);

        (await AccountTests.Codes(response, "photo")).Should().Equal("TooLarge");
    }

    private Task<HttpClient> AdminAsync() => factory.AdminAsync("photos-" + Guid.NewGuid().ToString("N")[..8]);

    [Fact]
    public async Task A_photo_without_date_is_dated_when_added_and_a_text_alone_is_a_piece()
    {
        var admin = await factory.AdminAsync("sans-date");
        var number = await CreateAsync(admin);

        var photo = await (await PostPieceAsync(admin, number, TestPhotos.Jpeg())).Content.ReadFromJsonAsync<PieceDetail>(Ct);
        var text = await (await PostPieceAsync(admin, number, null, "Toujours cassée ce matin")).Content.ReadFromJsonAsync<PieceDetail>(Ct);

        photo!.TakenAt.Should().BeNull();
        photo.ObservedAt.Should().Be(photo.AddedAt);
        text!.Photo.Should().BeNull();
        text.Text.Should().Be("Toujours cassée ce matin");
    }

    [Fact]
    public async Task A_piece_is_refused_when_empty_not_an_image_too_long_already_added_or_for_an_unknown_constat()
    {
        var admin = await factory.AdminAsync("refus");
        var number = await CreateAsync(admin);
        var photo = TestPhotos.Jpeg(colour: new SkiaSharp.SKColor(1, 2, 3));
        (await PostPieceAsync(admin, number, photo)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await AccountTests.Codes(await PostPieceAsync(admin, number, null, "  "), "piece")).Should().Equal("Empty");
        (await AccountTests.Codes(await PostPieceAsync(admin, number, "pas une image"u8.ToArray()), "photo")).Should().Equal("NotAnImage");
        (await AccountTests.Codes(await PostPieceAsync(admin, number, null, new string('x', 2001)), "text")).Should().Equal("TooLong");
        (await AccountTests.Codes(await PostPieceAsync(admin, number, []), "photo")).Should().Equal("EmptyFile");
        (await PostPieceAsync(admin, number, photo)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostPieceAsync(admin, 99999, TestPhotos.Jpeg())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Photos_are_served_to_a_session_only_and_by_their_exact_name()
    {
        var admin = await factory.AdminAsync("galerie");
        var number = await CreateAsync(admin);
        var piece = await (await PostPieceAsync(admin, number, TestPhotos.Jpeg(colour: new SkiaSharp.SKColor(9, 9, 9)))).Content.ReadFromJsonAsync<PieceDetail>(Ct);

        (await factory.Client().GetAsync($"/api/v1/photos/{piece!.Photo}", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.GetAsync($"/api/v1/photos/{piece.Photo!.ToUpperInvariant()}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync("/api/v1/photos/..%2Fadria-mo.db", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/v1/photos/{new string('0', 64)}.jpg", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var served = await admin.GetAsync($"/api/v1/photos/{piece.Photo}", Ct);
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
    }

    internal static async Task<int> CreateAsync(HttpClient admin, bool isPublic = true)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/constats", new { title = "Porte du hall forcée", category = "portes", isPublic }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConstatDetail>(Ct))!.Number;
    }

    internal static async Task<HttpResponseMessage> PostPieceAsync(HttpClient admin, int number, byte[]? photo, string? text = null)
    {
        using var form = new MultipartFormDataContent();
        if (photo is not null)
        {
            var file = new ByteArrayContent(photo);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "photo", "IMG_0001.jpg");
        }

        if (text is not null)
        {
            form.Add(new StringContent(text), "text");
        }

        return await admin.PostAsync($"/api/v1/constats/{number}/pieces", form, Ct);
    }
}
