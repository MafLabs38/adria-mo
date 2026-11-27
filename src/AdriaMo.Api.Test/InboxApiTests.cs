using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdriaMo.Api.Constats;
using AdriaMo.Api.Inbox;
using AdriaMo.Api.Photos;
using SkiaSharp;

namespace AdriaMo.Api.Test;

/// <summary>The inbox "À classer" (spec §4.1): photos sent from the phone wait there, in series, until they join a constat.</summary>
[Trait("Category", "Integration")]
public sealed class InboxApiTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_photo_sent_twice_waits_once_with_its_author()
    {
        var admin = await AdminAsync();
        var photo = Photo("2026:11:23 09:15:00", 1);

        var first = await PostAsync(admin, photo);
        var again = await PostAsync(admin, photo);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        again.StatusCode.Should().Be(HttpStatusCode.OK, "a phone sends again a photo whose answer was lost: nothing doubles");
        var item = await first.Content.ReadFromJsonAsync<InboxItem>(Ct);
        (await again.Content.ReadFromJsonAsync<InboxItem>(Ct))!.Id.Should().Be(item!.Id);
        item.TakenAt.Should().Be(new DateTimeOffset(2026, 11, 23, 9, 15, 0, TimeSpan.FromHours(1)));
        item.Author.Should().StartWith("inbox-");
        (await admin.GetAsync($"/api/v1/photos/{item.Thumbnail}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await ListAsync(admin);
        listed.Series.SelectMany(s => s.Photos).Should().ContainSingle(p => p.Id == item.Id);
        await DiscardAllAsync(admin, listed);
    }

    [Fact]
    public async Task Photos_are_sorted_in_series_by_the_date_they_were_taken()
    {
        var admin = await AdminAsync();
        await PostAsync(admin, Photo("2026:11:20 18:00:00", 2));
        await PostAsync(admin, Photo("2026:11:20 09:02:00", 3));
        await PostAsync(admin, Photo("2026:11:20 09:00:00", 4));
        await PostAsync(admin, Photo(null, 5));

        var listed = await ListAsync(admin);

        listed.Series.Select(s => s.Photos.Count).Should().Equal(1, 2);
        listed.Series[1].StartedAt.Should().Be(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(1)));
        listed.Series[1].EndedAt.Should().Be(new DateTimeOffset(2026, 11, 20, 9, 2, 0, TimeSpan.FromHours(1)));
        listed.Undated.Should().ContainSingle().Which.TakenAt.Should().BeNull();
        await DiscardAllAsync(admin, listed);
    }

    [Fact]
    public async Task A_series_becomes_a_new_constat_dated_by_its_first_photo()
    {
        var admin = await AdminAsync();
        var later = await ItemAsync(admin, Photo("2026:11:21 10:03:00", 6));
        var earlier = await ItemAsync(admin, Photo("2026:11:21 10:00:00", 7));

        var attached = await admin.PostAsJsonAsync("/api/v1/inbox/attach", new
        {
            photos = new[] { later.Id, earlier.Id, later.Id },
            create = new { title = "Fuite au plafond du hall", category = "humidite", location = "Hall A" },
            text = "  Tache qui s'agrandit ",
        }, Ct);

        attached.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await attached.Content.ReadFromJsonAsync<AttachResult>(Ct);
        result!.Attached.Should().Be(2);
        var constat = await admin.GetFromJsonAsync<ConstatDetail>($"/api/v1/constats/{result.Constat}", Ct);
        constat!.Title.Should().Be("Fuite au plafond du hall");
        constat.FirstObservedAt.Should().Be(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.FromHours(1)));
        constat.Pieces.Select(p => p.Photo).Should().Equal(earlier.Photo, later.Photo);
        constat.Pieces.Select(p => p.Text).Should().Equal("Tache qui s'agrandit", null);
        constat.Pieces[0].AddedAt.Should().BeCloseTo(earlier.AddedAt, TimeSpan.FromMilliseconds(1), "the deposit is the day the photo arrived");
        (await ListAsync(admin)).Series.SelectMany(s => s.Photos).Should().NotContain(p => p.Id == later.Id || p.Id == earlier.Id);
    }

    [Fact]
    public async Task A_photo_the_constat_already_holds_leaves_the_inbox_without_a_second_piece()
    {
        var admin = await AdminAsync();
        var number = await ConstatApiTests.CreateAsync(admin);
        var photo = Photo("2026:11:22 08:00:00", 8);
        var waiting = await ItemAsync(admin, photo);
        // Meanwhile the same photo was added to the constat from its own page.
        (await ConstatApiTests.PostPieceAsync(admin, number, photo)).EnsureSuccessStatusCode();
        var gone = await ItemAsync(admin, Photo("2026:11:22 08:01:00", 9));
        (await admin.DeleteAsync($"/api/v1/inbox/{gone.Id}", Ct)).EnsureSuccessStatusCode();

        var result = await Attach(admin, number, waiting.Id, gone.Id);

        result.Should().Be(new AttachResult(number, Attached: 0, AlreadyThere: 1, Missing: 1));
        (await admin.GetFromJsonAsync<ConstatDetail>($"/api/v1/constats/{number}", Ct))!.Pieces.Should().ContainSingle();
        (await ListAsync(admin)).Series.SelectMany(s => s.Photos).Should().NotContain(p => p.Id == waiting.Id);
    }

    [Fact]
    public async Task A_photo_already_in_a_constat_is_not_sent_to_the_inbox()
    {
        var admin = await AdminAsync();
        var number = await ConstatApiTests.CreateAsync(admin);
        var photo = Photo("2026:11:22 12:00:00", 10);
        (await ConstatApiTests.PostPieceAsync(admin, number, photo)).EnsureSuccessStatusCode();

        var sent = await PostAsync(admin, photo);

        sent.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await sent.Content.ReadAsStringAsync(Ct)).Should().Contain("AlreadyAdded");
    }

    [Fact]
    public async Task A_discarded_photo_and_its_files_are_gone()
    {
        var admin = await AdminAsync();
        var item = await ItemAsync(admin, Photo(null, 11));
        var originals = Path.Join(factory.DataFolder, PhotoIntake.OriginalFolder);
        var before = Directory.GetFiles(originals).Length;

        (await admin.DeleteAsync($"/api/v1/inbox/{item.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Directory.GetFiles(originals).Length.Should().Be(before - 1);
        (await admin.GetAsync($"/api/v1/photos/{item.Thumbnail}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.DeleteAsync($"/api/v1/inbox/{item.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task What_is_not_a_photo_is_refused()
    {
        var admin = await AdminAsync();

        (await AccountTests.Codes(await PostAsync(admin, "ceci n'est pas une image"u8.ToArray()), "photo")).Should().Equal("NotAnImage");
        (await AccountTests.Codes(await PostAsync(admin, []), "photo")).Should().Equal("EmptyFile");
        using var noFile = new MultipartFormDataContent { { new StringContent("x"), "text" } };
        (await AccountTests.Codes(await admin.PostAsync("/api/v1/inbox", noFile, Ct), "photo")).Should().Equal("Required");
    }

    [Fact]
    public async Task Attaching_needs_photos_and_one_target()
    {
        var admin = await AdminAsync();
        var item = await ItemAsync(admin, Photo(null, 12));
        var create = new { title = "Ampoule grillée", category = "eclairage" };

        (await AccountTests.Codes(await AttachRaw(admin, new { photos = Array.Empty<int>(), constat = 1 }), "photos")).Should().Equal("Required");
        (await AccountTests.Codes(await AttachRaw(admin, new { photos = Enumerable.Range(1, 201).ToArray(), constat = 1 }), "photos")).Should().Equal("TooMany");
        (await AccountTests.Codes(await AttachRaw(admin, new { photos = new[] { item.Id } }), "constat")).Should().Equal("Required");
        (await AccountTests.Codes(await AttachRaw(admin, new { photos = new[] { item.Id }, constat = 1, create }), "constat")).Should().Equal("Required");
        (await AccountTests.Codes(await AttachRaw(admin, new { photos = new[] { item.Id }, create = new { title = "", category = "eclairage" } }), "title"))
            .Should().Equal("Required");
        (await AccountTests.Codes(await AttachRaw(admin, new { photos = new[] { item.Id }, create, text = new string('x', 2001) }), "text"))
            .Should().Equal("TooLong");
        (await AttachRaw(admin, new { photos = new[] { item.Id }, constat = 99999 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AttachRaw(admin, new { photos = new[] { 99999 }, create })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ListAsync(admin)).Undated.Should().Contain(p => p.Id == item.Id, "nothing was attached");
        (await admin.DeleteAsync($"/api/v1/inbox/{item.Id}", Ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_inbox_is_for_administrators_only()
    {
        (await factory.Client().GetAsync("/api/v1/inbox", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var chef = await factory.SuperAdminAsync();
        (await chef.PostAsJsonAsync("/api/v1/users", new { userName = "nouveau-inbox", temporaryPassword = "provisoire du chef" }, Ct)).EnsureSuccessStatusCode();
        var temporary = await factory.LoggedInAsync("nouveau-inbox", "provisoire du chef");

        (await temporary.GetAsync("/api/v1/inbox", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<HttpClient> AdminAsync() => factory.AdminAsync("inbox-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>A JPEG of its own colour, so each photo of a test has its own content.</summary>
    private static byte[] Photo(string? takenAt, byte shade) => TestPhotos.Jpeg(takenAt: takenAt, colour: new SKColor(shade, (byte)(shade * 7), 90));

    private static async Task<HttpResponseMessage> PostAsync(HttpClient admin, byte[] photo)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(photo);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "photo", "IMG_0001.jpg");
        return await admin.PostAsync("/api/v1/inbox", form, Ct);
    }

    private static async Task<InboxItem> ItemAsync(HttpClient admin, byte[] photo)
    {
        var response = await PostAsync(admin, photo);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InboxItem>(Ct))!;
    }

    private static async Task<InboxListing> ListAsync(HttpClient admin) => (await admin.GetFromJsonAsync<InboxListing>("/api/v1/inbox", Ct))!;

    private static Task<HttpResponseMessage> AttachRaw(HttpClient admin, object request) => admin.PostAsJsonAsync("/api/v1/inbox/attach", request, Ct);

    private static async Task<AttachResult> Attach(HttpClient admin, int number, params int[] photos)
    {
        var response = await AttachRaw(admin, new { photos, constat = number });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachResult>(Ct))!;
    }

    /// <summary>The inbox is shared by the tests of the class: each test leaves it as it found it.</summary>
    private static async Task DiscardAllAsync(HttpClient admin, InboxListing listing)
    {
        foreach (var photo in listing.Series.SelectMany(s => s.Photos).Concat(listing.Undated))
        {
            (await admin.DeleteAsync($"/api/v1/inbox/{photo.Id}", Ct)).EnsureSuccessStatusCode();
        }
    }
}
