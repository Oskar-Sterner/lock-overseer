using System.Net.Http.Json;
using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class ReconcileDivergenceTests : IClassFixture<MockApiContainerFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly MockApiContainerFixture _api;
    private readonly OverseerHostFixture _host;
    public ReconcileDivergenceTests(MockApiContainerFixture api, OverseerHostFixture host)
    {
        _api = api;
        _host = host;
        _api.DockerAvailable.ShouldBeTrue(_api.UnavailableReason ?? "MockAPI failed to start");
        _host.UseAuthority(_api.BaseUri, _api.ApiKey);
    }

    [Fact]
    public async Task Out_of_band_insert_converges_within_one_reconcile_cycle()
    {
        _host.Service.IsBanned(76561198000000555).ShouldBeFalse();

        using var http = new HttpClient { BaseAddress = _api.BaseUri };
        http.DefaultRequestHeaders.Add("X-API-Key", _api.ApiKey);
        var post = await http.PostAsJsonAsync("/bans", new
        {
            steam_id = 76561198000000555L,
            reason = "oob",
            issued_by = new { steam_id = (long?)null, label = "test" },
        });
        post.IsSuccessStatusCode.ShouldBeTrue(await post.Content.ReadAsStringAsync());

        await _host.ForceReconcileAsync();

        _host.Service.IsBanned(76561198000000555).ShouldBeTrue();
    }
}
