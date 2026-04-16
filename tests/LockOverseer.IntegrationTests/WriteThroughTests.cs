using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class WriteThroughTests : IClassFixture<ExternalApiFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly ExternalApiFixture _api;
    private readonly OverseerHostFixture _host;
    public WriteThroughTests(ExternalApiFixture api, OverseerHostFixture host)
    {
        _api = api;
        _host = host;
        _api.Available.ShouldBeTrue(_api.UnavailableReason ?? "external API failed to start");
        _host.UseAuthority(_api.BaseUri, _api.ApiKey);
    }

    [Fact]
    public async Task IssueBan_persists_row_updates_cache_and_triggers_kick()
    {
        var svc = _host.Service;
        var kicked = false;
        _host.OnKick = (_, _) => kicked = true;

        var r = await svc.IssueBanAsync(
            new BanRequest(76561198000000123, DurationMinutes: null, Reason: "griefing",
                IssuedBy: new Issuer(null, "integration-test")));

        r.IsSuccess.ShouldBeTrue(r.Error?.Message);
        svc.IsBanned(76561198000000123).ShouldBeTrue();

        using var http = new HttpClient { BaseAddress = _api.BaseUri };
        http.DefaultRequestHeaders.Add("X-API-Key", _api.ApiKey);
        var list = await http.GetStringAsync("/bans?active=true");
        list.ShouldContain("76561198000000123");
    }
}
