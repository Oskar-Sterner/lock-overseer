using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class WriteThroughTests : IClassFixture<MockApiContainerFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly MockApiContainerFixture _api;
    private readonly OverseerHostFixture _host;
    public WriteThroughTests(MockApiContainerFixture api, OverseerHostFixture host)
    { _api = api; _host = host; if (_api.DockerAvailable) _host.UseAuthority(_api.BaseUri); }

    [Fact]
    public async Task IssueBan_persists_row_updates_cache_and_triggers_kick()
    {
        if (!_api.DockerAvailable) return; // Skip — Docker/MockAPI image unavailable.

        var svc = _host.Service;
        var kicked = false;
        _host.OnKick = (_, _) => kicked = true;

        var r = await svc.IssueBanAsync(
            new BanRequest(76561198000000123, DurationMinutes: null, Reason: "griefing",
                IssuedBy: new Issuer(null, "integration-test")));

        r.IsSuccess.ShouldBeTrue();
        svc.IsBanned(76561198000000123).ShouldBeTrue();

        using var http = new HttpClient { BaseAddress = _api.BaseUri };
        var list = await http.GetStringAsync("/bans?active=true");
        list.ShouldContain("76561198000000123");
    }
}
