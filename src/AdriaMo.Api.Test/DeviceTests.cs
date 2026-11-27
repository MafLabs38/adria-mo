using System.Net;
using System.Net.Http.Json;
using AdriaMo.Api.Accounts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AdriaMo.Api.Test;

/// <summary>Pairing a phone by QR code (spec §4.1): a code used once, a session of its own, revocable.</summary>
[Trait("Category", "Integration")]
public sealed class DeviceTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_scanned_code_logs_the_phone_in_once_without_the_password()
    {
        var admin = await factory.AdminAsync("appaire", "la porte du hall b");
        var code = await CodeAsync(admin, "  Téléphone d'Éric ");
        var phone = factory.Client();

        var paired = await phone.PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct);

        paired.StatusCode.Should().Be(HttpStatusCode.OK);
        (await paired.Content.ReadFromJsonAsync<Me>(Ct))!.UserName.Should().Be("appaire");
        (await phone.GetAsync("/api/v1/inbox", Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "the phone acts for its account");
        var again = await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct);
        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a code opens one session only");
        var devices = await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct);
        devices.Should().ContainSingle().Which.Should().Match<DeviceSummary>(d => d.Name == "Téléphone d'Éric" && d.PairedAt != null && !d.IsCurrent);
        (await phone.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().ContainSingle(d => d.IsCurrent);
    }

    [Fact]
    public async Task The_session_of_a_phone_lasts_ninety_days_without_use_in_a_secure_cookie()
    {
        await using var clocked = new ClockFactory();
        var code = await CodeAsync(await clocked.AdminAsync("longtemps"));
        var phone = clocked.Client();

        var paired = await phone.PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct);

        var cookie = paired.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-adriamo=", StringComparison.Ordinal));
        cookie.Should().Contain("secure").And.Contain("httponly").And.Contain("samesite=strict");
        var expires = DateTimeOffset.Parse(cookie.Split("expires=")[1].Split(';')[0], System.Globalization.CultureInfo.InvariantCulture);
        expires.Should().Be(clocked.Clock.GetUtcNow().AddDays(90));
        // Used every two months, it stays logged in: the 90 days start again at each use.
        clocked.Clock.Advance(TimeSpan.FromDays(60));
        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        clocked.Clock.Advance(TimeSpan.FromDays(60));
        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        // Unused for more than 90 days, it is logged out.
        clocked.Clock.Advance(TimeSpan.FromDays(91));
        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_code_opens_one_session_even_scanned_twice_at_the_same_instant()
    {
        var code = await CodeAsync(await factory.AdminAsync("simultane"));

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct)));

        answers.Count(a => a.StatusCode == HttpStatusCode.OK).Should().Be(1);
        answers.Where(a => a.StatusCode != HttpStatusCode.OK).Should().OnlyContain(a => a.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_code_shown_before_a_password_change_or_on_a_locked_account_is_refused()
    {
        var admin = await factory.AdminAsync("change-avant", "la porte du hall b");
        var stale = await CodeAsync(admin);
        (await admin.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "la porte du hall b", newPassword = "le local à vélos est plein" }, Ct))
            .EnsureSuccessStatusCode();

        (await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = stale.Code }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var locked = await factory.AdminAsync("bloque");
        var code = await CodeAsync(locked);
        for (var i = 0; i < 5; i++)
        {
            await factory.Client().PostAsJsonAsync("/api/v1/auth/login", new { userName = "bloque", password = "pas le bon mot de passe" }, Ct);
        }

        (await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_account_with_a_temporary_password_does_not_pair_a_phone()
    {
        var chef = await factory.SuperAdminAsync();
        (await chef.PostAsJsonAsync("/api/v1/users", new { userName = "provisoire-tel", temporaryPassword = "provisoire du chef" }, Ct)).EnsureSuccessStatusCode();
        var temporary = await factory.LoggedInAsync("provisoire-tel", "provisoire du chef");

        (await temporary.PostAsJsonAsync("/api/v1/devices", new { name = "Téléphone" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_new_code_purges_the_phones_removed_earlier()
    {
        var admin = await factory.AdminAsync("menage");
        var (_, removed) = await PairAsync(admin);
        (await admin.DeleteAsync($"/api/v1/devices/{removed}", Ct)).EnsureSuccessStatusCode();

        await CodeAsync(admin);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.AdriaMoDbContext>();
        db.Devices.Any(d => d.Id == removed).Should().BeFalse();
    }

    [Fact]
    public async Task A_phone_does_not_pair_another_device()
    {
        var (phone, _) = await PairAsync(await factory.AdminAsync("pas-de-relais"));

        (await phone.PostAsJsonAsync("/api/v1/devices", new { name = "Second" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_revoked_phone_loses_its_session_at_its_next_request()
    {
        var admin = await factory.AdminAsync("revoque");
        var (phone, id) = await PairAsync(admin);

        (await admin.DeleteAsync($"/api/v1/devices/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "the computer's session is not the phone's");
        (await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().BeEmpty();
        (await admin.DeleteAsync($"/api/v1/devices/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_administrator_cannot_revoke_the_phone_of_another()
    {
        var owner = await factory.AdminAsync("proprietaire");
        var other = await factory.AdminAsync("voisin-curieux");
        var (phone, id) = await PairAsync(owner);

        (await other.DeleteAsync($"/api/v1/devices/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().BeEmpty();
        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_phone_that_logs_out_is_unpaired()
    {
        var admin = await factory.AdminAsync("deconnecte");
        var (phone, _) = await PairAsync(admin);

        (await phone.PostAsync("/api/v1/auth/logout", null, Ct)).EnsureSuccessStatusCode();

        (await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_reset_password_revokes_every_phone_and_voids_the_waiting_code()
    {
        var admin = await factory.AdminAsync("perdu");
        var (phone, _) = await PairAsync(admin);
        var waiting = await CodeAsync(admin, "Tablette");
        var chef = await factory.SuperAdminAsync();
        var users = await chef.GetFromJsonAsync<UserSummary[]>("/api/v1/users", Ct);

        (await chef.PostAsJsonAsync($"/api/v1/users/{users!.Single(u => u.UserName == "perdu").Id}/password", new { temporaryPassword = "nouveau provisoire" }, Ct))
            .EnsureSuccessStatusCode();

        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = waiting.Code }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_phone_that_changes_the_password_stays_paired_and_ends_the_other_phones()
    {
        var admin = await factory.AdminAsync("change", "la porte du hall b");
        var (first, firstId) = await PairAsync(admin);
        var (second, _) = await PairAsync(admin);

        (await first.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "la porte du hall b", newPassword = "le local à vélos est plein" }, Ct))
            .EnsureSuccessStatusCode();

        (await second.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var listed = await first.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct);
        listed.Should().ContainSingle().Which.Should().Match<DeviceSummary>(d => d.Id == firstId && d.IsCurrent);
        (await admin.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await first.DeleteAsync($"/api/v1/devices/{firstId}", Ct)).EnsureSuccessStatusCode();
        (await first.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the phone kept its identity after the change");
    }

    [Fact]
    public async Task A_new_code_replaces_the_one_not_scanned()
    {
        var admin = await factory.AdminAsync("deux-codes");
        var first = await CodeAsync(admin, "Premier");
        await CodeAsync(admin, "Second");

        (await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = first.Code }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().ContainSingle().Which.Name.Should().Be("Second");
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas-un-code")]
    public async Task An_unknown_code_is_refused_with_the_same_answer(string code)
    {
        var refused = await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Length == 0 ? null : code }, Ct);

        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadAsStringAsync(Ct)).Should().Contain("plus valable");
        (await factory.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = new string('a', 65) }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_a_logged_in_administrator_asks_for_a_code_and_names_the_phone()
    {
        (await factory.Client().PostAsJsonAsync("/api/v1/devices", new { name = "x" }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var admin = await factory.AdminAsync("nomme");

        var unnamed = await admin.PostAsJsonAsync("/api/v1/devices", new { name = " " }, Ct);

        (await AccountTests.Codes(unnamed, "name")).Should().Equal("Required");
    }

    [Fact]
    public async Task A_code_expires_after_ten_minutes()
    {
        await using var clocked = new ClockFactory();
        var admin = await clocked.AdminAsync("lent");
        var code = await CodeAsync(admin);
        code.ExpiresAt.Should().Be(clocked.Clock.GetUtcNow().AddMinutes(10));
        clocked.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        (await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().ContainSingle("the code is still listed until it expires");

        clocked.Clock.Advance(TimeSpan.FromMinutes(10));

        (await clocked.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.GetFromJsonAsync<DeviceSummary[]>("/api/v1/devices", Ct)).Should().BeEmpty("an expired code is not listed");
    }

    [Fact]
    public async Task A_paired_phone_stays_logged_in_when_the_back_end_restarts()
    {
        var root = Path.Join(Path.GetTempPath(), "adria-mo-restart-" + Guid.NewGuid().ToString("N"));
        try
        {
            string session;
            await using (var before = new FolderFactory(root))
            {
                var code = await CodeAsync(await before.AdminAsync("redemarre"));
                var paired = await before.Client().PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct);
                session = paired.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-adriamo=", StringComparison.Ordinal)).Split(';')[0];
                // In the data folder (the volume), not in the home folder of the account, which a container does not keep.
                Directory.GetFiles(Path.Join(before.DataFolder, "keys"), "key-*.xml").Should().NotBeEmpty();
            }

            await using var after = new FolderFactory(root);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Add("Cookie", session);

            (await after.Client().SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.OK, "the keys of the cookies are in the data folder");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private async Task<(HttpClient Phone, string Id)> PairAsync(HttpClient admin)
    {
        var code = await CodeAsync(admin);
        var phone = factory.Client();
        (await phone.PostAsJsonAsync("/api/v1/auth/pair", new { code = code.Code }, Ct)).EnsureSuccessStatusCode();
        return (phone, code.Id);
    }

    private static async Task<PairingCode> CodeAsync(HttpClient admin, string name = "Téléphone")
    {
        var response = await admin.PostAsJsonAsync("/api/v1/devices", new { name }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PairingCode>(Ct))!;
    }

    /// <summary>A back end on folders that outlive it, deleted by the test.</summary>
    private sealed class FolderFactory(string root) : AdriaMoFactory(root, keepFolders: true);

    private sealed class ClockFactory : AdriaMoFactory
    {
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 27, 14, 0, 0, TimeSpan.Zero));

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
        }
    }
}
