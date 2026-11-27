using System.Net;
using System.Net.Http.Json;

namespace AdriaMo.Api.Test;

[Trait("Category", "Integration")]
public sealed class StartupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_attempts_from_one_address_are_limited()
    {
        await using var factory = new SettingsFactory(new() { ["Auth:LoginAttemptsPerMinute"] = "2" });
        var client = factory.Client();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "x", password = "y" }, Ct)).StatusCode);
        }

        statuses.Should().Equal(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_super_admin_password_breaking_the_rules_stops_the_start()
    {
        await using var factory = new SettingsFactory([], bootstrapPassword: "court");

        var start = factory.Client;
        start.Should().Throw<InvalidOperationException>().WithMessage("*TooShort*");
    }

    [Fact]
    public async Task An_unknown_time_zone_stops_the_start()
    {
        await using var factory = new SettingsFactory(new() { ["Building:TimeZone"] = "Mars/Olympus" });

        var start = factory.Client;
        start.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    [Fact]
    public async Task Without_bootstrap_settings_the_back_end_starts_without_account()
    {
        await using var factory = new SettingsFactory(new() { ["Auth:BootstrapUserName"] = "", ["Auth:BootstrapPasswordFile"] = "" });
        var response = await factory.Client().PostAsJsonAsync("/api/v1/auth/login", new { userName = AdriaMoFactory.SuperAdmin, password = AdriaMoFactory.SuperAdminPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_user_name_without_its_password_file_stops_the_start()
    {
        await using var factory = new SettingsFactory(new() { ["Auth:BootstrapPasswordFile"] = "" });

        var start = factory.Client;
        start.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>();
    }

    private sealed class SettingsFactory(Dictionary<string, string?> settings, string bootstrapPassword = AdriaMoFactory.SuperAdminPassword)
        : AdriaMoFactory
    {
        protected override IDictionary<string, string?> Settings => settings;

        protected override string BootstrapPassword => bootstrapPassword;
    }
}
