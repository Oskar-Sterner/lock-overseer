using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class MockApiContainerSmokeTests : IClassFixture<MockApiContainerFixture>
{
    private readonly MockApiContainerFixture _fx;
    public MockApiContainerSmokeTests(MockApiContainerFixture fx) => _fx = fx;

    [Fact]
    public async Task Health_endpoint_is_reachable()
    {
        _fx.DockerAvailable.ShouldBeTrue(_fx.UnavailableReason ?? "MockAPI failed to start");

        using var http = new HttpClient { BaseAddress = _fx.BaseUri };
        var r = await http.GetAsync("/health");
        ((int)r.StatusCode).ShouldBe(200);
    }

    [Fact]
    public async Task Bans_endpoint_requires_api_key()
    {
        _fx.DockerAvailable.ShouldBeTrue(_fx.UnavailableReason ?? "MockAPI failed to start");

        using var http = new HttpClient { BaseAddress = _fx.BaseUri };
        var unauth = await http.GetAsync("/bans");
        ((int)unauth.StatusCode).ShouldBe(401);

        using var authed = new HttpClient { BaseAddress = _fx.BaseUri };
        authed.DefaultRequestHeaders.Add("X-API-Key", _fx.ApiKey);
        var ok = await authed.GetAsync("/bans");
        ((int)ok.StatusCode).ShouldBe(200);
    }
}
