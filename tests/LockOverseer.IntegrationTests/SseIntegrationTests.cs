using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Caching;
using LockOverseer.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class SseIntegrationTests
    : IClassFixture<MockApiContainerFixture>, IClassFixture<OverseerHostFixture>
{
    private readonly MockApiContainerFixture _mock;
    private readonly OverseerHostFixture _host;
    private long _lastKickedSteamId;
    private string? _lastKickReason;

    public SseIntegrationTests(MockApiContainerFixture mock, OverseerHostFixture host)
    {
        _mock = mock;
        _host = host;
        _host.OnKick = (sid, reason) =>
        {
            Interlocked.Exchange(ref _lastKickedSteamId, sid);
            _lastKickReason = reason;
        };
        _host.UseAuthority(_mock.BaseUri, _mock.ApiKey);
    }

    [Fact]
    public async Task Web_issued_ban_propagates_to_plugin_cache_and_triggers_kick_within_2s()
    {
        if (!_mock.DockerAvailable)
        {
            // MockAPI subprocess couldn't start (missing uv, etc.) — skip.
            return;
        }

        const long steamId = 76561198000099999L;
        using var http = new HttpClient { BaseAddress = _mock.BaseUri };
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _mock.ApiKey);

        var resp = await http.PostAsJsonAsync("/bans", new
        {
            steam_id = steamId,
            duration_minutes = 60,
            reason = "sse-test",
            issued_by = new { steam_id = (long?)null, label = "sys" },
        });
        resp.EnsureSuccessStatusCode();

        var cache = _host.Services.GetRequiredService<AuthorityCache>();

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cache.IsBanned(steamId) &&
                Interlocked.Read(ref _lastKickedSteamId) == steamId)
            {
                cache.IsBanned(steamId).ShouldBeTrue();
                _lastKickReason.ShouldBe("sse-test");
                return;
            }
            await Task.Delay(50);
        }

        Assert.Fail(
            $"SSE propagation failed: cache.IsBanned(steamId)={cache.IsBanned(steamId)}, " +
            $"lastKicked={Interlocked.Read(ref _lastKickedSteamId)} (expected {steamId})");
    }
}
