using System.Net;
using System.Net.Http.Json;

namespace AdriaMo.Api.Test;

[Trait("Category", "Integration")]
public sealed class ApiSmokeTests(AdriaMoFactory factory) : IClassFixture<AdriaMoFactory>
{
    [Fact]
    public async Task The_liveness_probe_answers()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_api_says_its_name_and_version()
    {
        var info = await factory.CreateClient().GetFromJsonAsync<AppInfo>(new Uri("/api/v1/info", UriKind.Relative), TestContext.Current.CancellationToken);

        info.Should().NotBeNull();
        info!.Name.Should().Be("Adria MO");
        info.Version.Should().NotBeNullOrWhiteSpace().And.NotBe("unknown");
    }
}
