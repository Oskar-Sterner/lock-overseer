using System.Net.Http.Json;
using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class HttpSurfaceTests : IClassFixture<ExternalApiFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly ExternalApiFixture _api;
    private readonly OverseerHostFixture _host;
    public HttpSurfaceTests(ExternalApiFixture api, OverseerHostFixture host)
    {
        _api = api;
        _host = host;
        _api.Available.ShouldBeTrue(_api.UnavailableReason ?? "external API failed to start");
        _host.UseAuthority(_api.BaseUri, _api.ApiKey);
    }

    [Fact]
    public async Task Post_v1_bans_via_plugin_http_persists_in_authority_api()
    {
        using var plugin = _host.CreatePluginHttpClient();
        var r = await plugin.PostAsJsonAsync("/v1/bans",
            new { steamId = 76561198000000777L, issuedByLabel = "http-test", reason = "e2e" });
        ((int)r.StatusCode).ShouldBe(201, await r.Content.ReadAsStringAsync());

        using var api = new HttpClient { BaseAddress = _api.BaseUri };
        api.DefaultRequestHeaders.Add("X-API-Key", _api.ApiKey);
        var list = await api.GetStringAsync("/bans?active=true");
        list.ShouldContain("76561198000000777");
    }
}
