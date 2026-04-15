using System.Net.Http.Json;
using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class HttpSurfaceTests : IClassFixture<MockApiContainerFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly MockApiContainerFixture _api;
    private readonly OverseerHostFixture _host;
    public HttpSurfaceTests(MockApiContainerFixture api, OverseerHostFixture host)
    { _api = api; _host = host; if (_api.DockerAvailable) _host.UseAuthority(_api.BaseUri); }

    [Fact]
    public async Task Post_v1_bans_via_plugin_http_persists_in_authority_api()
    {
        if (!_api.DockerAvailable) return;

        using var plugin = _host.CreatePluginHttpClient();
        var r = await plugin.PostAsJsonAsync("/v1/bans",
            new { steamId = 76561198000000777L, issuedByLabel = "http-test", reason = "e2e" });
        ((int)r.StatusCode).ShouldBe(201);

        using var api = new HttpClient { BaseAddress = _api.BaseUri };
        var list = await api.GetStringAsync("/bans?active=true");
        list.ShouldContain("76561198000000777");
    }
}
