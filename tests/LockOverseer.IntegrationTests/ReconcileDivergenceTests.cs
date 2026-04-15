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
    { _api = api; _host = host; if (_api.DockerAvailable) _host.UseAuthority(_api.BaseUri); }

    [Fact]
    public async Task Out_of_band_insert_converges_within_one_reconcile_cycle()
    {
        if (!_api.DockerAvailable) return;

        _host.Service.IsBanned(76561198000000555).ShouldBeFalse();

        using var http = new HttpClient { BaseAddress = _api.BaseUri };
        await http.PostAsJsonAsync("/bans", new { steam_id = 76561198000000555L, reason = "oob", issued_by_label = "test" });

        await _host.ForceReconcileAsync();

        _host.Service.IsBanned(76561198000000555).ShouldBeTrue();
    }
}
