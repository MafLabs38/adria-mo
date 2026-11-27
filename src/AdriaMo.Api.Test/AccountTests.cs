using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdriaMo.Api.Accounts;
using AdriaMo.Api.Data;
using AdriaMo.Core.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace AdriaMo.Api.Test;

[Trait("Category", "Integration")]
public sealed class AccountTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    private const string Temporary = "provisoire du chef";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_super_admin_created_at_start_logs_in_with_a_strict_session_cookie()
    {
        var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = AdriaMoFactory.SuperAdmin, password = AdriaMoFactory.SuperAdminPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-adriamo=", StringComparison.Ordinal)).ToLowerInvariant();
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/").And.Contain("expires=");
        var me = await client.GetFromJsonAsync<Me>("/api/v1/auth/me", Ct);
        me.Should().BeEquivalentTo(new Me(AdriaMoFactory.SuperAdmin, ["super-admin"], false, "Europe/Paris"));
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_user_get_the_same_answer()
    {
        var client = factory.Client();
        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = AdriaMoFactory.SuperAdmin, password = "pas le bon mot de passe" }, Ct);
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "personne", password = "pas le bon mot de passe" }, Ct);
        var empty = await client.PostAsJsonAsync("/api/v1/auth/login", new { }, Ct);

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        empty.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var wrongBody = JsonDocument.Parse(await wrong.Content.ReadAsStringAsync(Ct)).RootElement;
        var unknownBody = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(Ct)).RootElement;
        unknownBody.GetProperty("title").GetString().Should().Be(wrongBody.GetProperty("title").GetString());
        unknownBody.GetProperty("detail").GetString().Should().Be(wrongBody.GetProperty("detail").GetString());
        wrongBody.GetRawText().Should().NotContain("stack").And.NotContain("Exception");
    }

    [Fact]
    public async Task Without_a_session_the_administration_answers_401_and_never_redirects()
    {
        var client = factory.Client();

        (await client.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/constats", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/users", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_change_without_the_application_header_is_refused()
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = AdriaMoFactory.SuperAdmin, password = AdriaMoFactory.SuperAdminPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await client.GetAsync("/api/v1/info", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/API/v1/auth/login")]
    [InlineData("/Api/V1/constats/1/pieces")]
    [InlineData("/anything")]
    public async Task The_application_header_is_required_whatever_the_path_and_its_case(string path)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync(path, new { userName = AdriaMoFactory.SuperAdmin, password = AdriaMoFactory.SuperAdminPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Four_failures_do_not_lock_and_a_success_starts_the_count_again()
    {
        await factory.AdminAsync("presque", "la porte du hall b");
        var client = factory.Client();
        async Task FailAsync(int times)
        {
            for (var i = 0; i < times; i++)
            {
                await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "presque", password = "pas le bon mot de passe" }, Ct);
            }
        }

        await FailAsync(4);
        await factory.LoggedInAsync("presque", "la porte du hall b");
        await FailAsync(4);
        var again = await factory.LoggedInAsync("presque", "la porte du hall b");

        (await again.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET", "/api/v1/constats")]
    [InlineData("POST", "/api/v1/constats")]
    [InlineData("GET", "/api/v1/constats/1")]
    [InlineData("PUT", "/api/v1/constats/1")]
    [InlineData("POST", "/api/v1/constats/1/pieces")]
    [InlineData("GET", "/api/v1/categories")]
    [InlineData("GET", "/api/v1/publication")]
    [InlineData("GET", "/api/v1/users")]
    public async Task A_temporary_password_opens_nothing_and_no_session_opens_nothing(string method, string path)
    {
        var chef = await factory.SuperAdminAsync();
        var userName = "provisoire-" + Guid.NewGuid().ToString("N")[..8];
        (await chef.PostAsJsonAsync("/api/v1/users", new { userName, temporaryPassword = Temporary }, Ct)).EnsureSuccessStatusCode();
        var temporary = await factory.LoggedInAsync(userName, Temporary);

        (await temporary.SendAsync(Request(method, path), Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.Client().SendAsync(Request(method, path), Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_administrator_cannot_manage_accounts()
    {
        var admin = await factory.AdminAsync("simple-admin");

        (await admin.PostAsJsonAsync("/api/v1/users", new { userName = "intrus", temporaryPassword = Temporary }, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/v1/users/x/password", new { temporaryPassword = Temporary }, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        Content = method switch
        {
            "GET" => null,
            _ when path.EndsWith("/pieces", StringComparison.Ordinal) => new MultipartFormDataContent { { new StringContent("x"), "text" } },
            _ => JsonContent.Create(new { title = "x", category = "portes" }),
        },
    };

    [Fact]
    public async Task A_new_administrator_must_choose_a_password_before_anything_else()
    {
        var chef = await factory.SuperAdminAsync();
        var created = await chef.PostAsJsonAsync("/api/v1/users", new { userName = "nouveau", temporaryPassword = Temporary }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var admin = await factory.LoggedInAsync("nouveau", Temporary);
        (await admin.GetFromJsonAsync<Me>("/api/v1/auth/me", Ct))!.MustChangePassword.Should().BeTrue();
        (await admin.GetAsync("/api/v1/constats", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var changed = await admin.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = Temporary, newPassword = "ma propre phrase à moi" }, Ct);

        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<Me>(Ct)).Should().BeEquivalentTo(new Me("nouveau", ["admin"], false, "Europe/Paris"));
        (await admin.GetAsync("/api/v1/constats", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/users", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await factory.LoggedInAsync("nouveau", "ma propre phrase à moi");
    }

    [Theory]
    [InlineData("pas le provisoire", "une phrase assez longue", "PasswordMismatch")]
    [InlineData(Temporary, "court", "TooShort")]
    [InlineData(Temporary, "regles-de-regles", "ContainsUserName")]
    [InlineData(Temporary, "motdepasse123", "TooCommon")]
    [InlineData(Temporary, Temporary, "SameAsCurrent")]
    public async Task A_change_of_password_says_what_is_wrong(string current, string next, string code)
    {
        var chef = await factory.SuperAdminAsync();
        var userName = "regles-" + code.ToLowerInvariant();
        (await chef.PostAsJsonAsync("/api/v1/users", new { userName, temporaryPassword = Temporary }, Ct)).EnsureSuccessStatusCode();
        var admin = await factory.LoggedInAsync(userName, Temporary);
        var nextPassword = next.Replace("regles", userName, StringComparison.Ordinal);

        var response = await admin.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = current, newPassword = nextPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Codes(response, "newPassword")).Should().Contain(code);
    }

    [Fact]
    public async Task Changing_ones_password_ends_the_other_sessions()
    {
        var laptop = await factory.AdminAsync("deux-sessions", "la porte du hall b");
        var phone = await factory.LoggedInAsync("deux-sessions", "la porte du hall b");

        var changed = await laptop.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "la porte du hall b", newPassword = "la porte du hall c" }, Ct);

        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await phone.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await laptop.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_a_reset_by_the_super_admin_unlocks_it()
    {
        await factory.AdminAsync("distrait", "la porte du hall b");
        var client = factory.Client();
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "distrait", password = "pas le bon mot de passe" }, Ct);
        }

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "distrait", password = "la porte du hall b" }, Ct);
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var chef = await factory.SuperAdminAsync();
        var list = await chef.GetFromJsonAsync<UserSummary[]>("/api/v1/users", Ct);
        var user = list!.Single(u => u.UserName == "distrait");
        user.LockedOutUntil.Should().NotBeNull();

        var reset = await chef.PostAsJsonAsync($"/api/v1/users/{user.Id}/password", new { temporaryPassword = "un nouveau provisoire" }, Ct);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await factory.LoggedInAsync("distrait", "un nouveau provisoire");
        (await again.GetFromJsonAsync<Me>("/api/v1/auth/me", Ct))!.MustChangePassword.Should().BeTrue();
        (await chef.GetFromJsonAsync<UserSummary[]>("/api/v1/users", Ct))!.Single(u => u.UserName == "distrait").LockedOutUntil.Should().BeNull();
    }

    [Fact]
    public async Task A_reset_by_the_super_admin_ends_the_sessions_of_the_account()
    {
        var admin = await factory.AdminAsync("oublieux", "la porte du hall b");
        var chef = await factory.SuperAdminAsync();
        var id = (await chef.GetFromJsonAsync<UserSummary[]>("/api/v1/users", Ct))!.Single(u => u.UserName == "oublieux").Id;

        (await chef.PostAsJsonAsync($"/api/v1/users/{id}/password", new { temporaryPassword = "un nouveau provisoire" }, Ct)).EnsureSuccessStatusCode();

        (await admin.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_super_admin_gets_a_clear_answer_for_a_bad_account()
    {
        var chef = await factory.SuperAdminAsync();

        var weak = await chef.PostAsJsonAsync("/api/v1/users", new { userName = "faible", temporaryPassword = "court" }, Ct);
        var badName = await chef.PostAsJsonAsync("/api/v1/users", new { userName = "x", temporaryPassword = Temporary }, Ct);
        var spaces = await chef.PostAsJsonAsync("/api/v1/users", new { userName = "deux mots", temporaryPassword = Temporary }, Ct);
        var duplicate = await chef.PostAsJsonAsync("/api/v1/users", new { userName = AdriaMoFactory.SuperAdmin, temporaryPassword = Temporary }, Ct);
        (await duplicate.Content.ReadFromJsonAsync<string>(Ct)).Should().Be("DuplicateUserName");
        var unknown = await chef.PostAsJsonAsync("/api/v1/users/inconnu/password", new { temporaryPassword = Temporary }, Ct);

        (await Codes(weak, "temporaryPassword")).Should().Contain("TooShort");
        (await Codes(badName, "userName")).Should().Contain("InvalidUserName");
        (await Codes(spaces, "userName")).Should().Contain("InvalidUserName");
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_reset_with_a_weak_password_changes_nothing()
    {
        await factory.AdminAsync("prudent", "la porte du hall b");
        var chef = await factory.SuperAdminAsync();
        var id = (await chef.GetFromJsonAsync<UserSummary[]>("/api/v1/users", Ct))!.Single(u => u.UserName == "prudent").Id;

        var reset = await chef.PostAsJsonAsync($"/api/v1/users/{id}/password", new { temporaryPassword = "court" }, Ct);

        (await Codes(reset, "temporaryPassword")).Should().Contain("TooShort");
        await factory.LoggedInAsync("prudent", "la porte du hall b");
    }

    [Fact]
    public async Task Passwords_are_stored_as_a_salted_pbkdf2_sha512_hash_of_210000_iterations()
    {
        await factory.AdminAsync("hache", "la porte du hall b");
        using var scope = factory.Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<AdriaMoDbContext>().Users.Single(u => u.UserName == "hache");

        user.PasswordHash.Should().NotContain("porte");
        var hash = Convert.FromBase64String(user.PasswordHash!);
        hash[0].Should().Be(0x01, "format v3 of Identity");
        BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(1)).Should().Be(2, "HMAC-SHA512");
        BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(5)).Should().Be((uint)AccountRules.HashIterations);
        BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(9)).Should().Be(16, "a 128-bit salt");
        AccountRules.HashIterations.Should().Be(210_000);
    }

    [Fact]
    public async Task An_accent_typed_another_way_is_the_same_password()
    {
        // Set with "é" as one character, typed with "e" and a combining accent.
        await factory.AdminAsync("accent", "un \u00e9t\u00e9 au hall b");

        var client = await factory.LoggedInAsync("accent", "un e\u0301te\u0301 au hall b");

        (await client.GetFromJsonAsync<Me>("/api/v1/auth/me", Ct))!.UserName.Should().Be("accent");
    }

    [Fact]
    public async Task Logging_out_ends_the_session()
    {
        var chef = await factory.SuperAdminAsync();

        (await chef.PostAsync("/api/v1/auth/logout", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await chef.GetAsync("/api/v1/auth/me", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    internal static async Task<string[]> Codes(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. body.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!)];
    }
}
