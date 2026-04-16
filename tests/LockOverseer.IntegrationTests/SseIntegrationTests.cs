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

    [Fact]
    public async Task Web_issued_role_assignment_hydrates_connected_player_within_2s()
    {
        if (!_mock.DockerAvailable)
        {
            return;
        }

        const long steamId = 76561198000077777L;

        // Pre-populate ConnectedPlayerState to simulate a connected player.
        var cache = _host.Services.GetRequiredService<AuthorityCache>();
        cache.SetConnectedState(steamId, new ConnectedPlayerState(
            RoleName: null,
            EffectiveFlags: System.Collections.Frozen.FrozenSet<string>.Empty));

        using var http = new HttpClient { BaseAddress = _mock.BaseUri };
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _mock.ApiKey);

        // 1. Ensure a role exists.
        var roleResp = await http.PostAsJsonAsync("/roles", new
        {
            name = "sse-test-role",
            description = (string?)null,
            priority = 50,
            flags = new[] { "overseer.kick" },
        });
        // Accept 201 Created or 409 Conflict (role may already exist from a prior run in this fixture).
        if (roleResp.StatusCode != System.Net.HttpStatusCode.Created &&
            roleResp.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            roleResp.EnsureSuccessStatusCode();  // throws
        }

        // 2. Assign it to the (simulated) connected player.
        // Endpoint: POST /players/{steamId}/roles, body: {role_name, duration_minutes, assigned_by}.
        var assignResp = await http.PostAsJsonAsync($"/players/{steamId}/roles", new
        {
            role_name = "sse-test-role",
            duration_minutes = (int?)null,
            assigned_by = new { steam_id = (long?)null, label = "sse-integration-test" },
        });
        assignResp.EnsureSuccessStatusCode();

        // 3. Wait for SSE to deliver role_assignment.created → HydrateConnectedAsync →
        //    ConnectedPlayerState.RoleName becomes "sse-test-role".
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cache.GetRole(steamId) == "sse-test-role")
            {
                cache.GetRole(steamId).ShouldBe("sse-test-role");
                return;
            }
            await Task.Delay(50);
        }

        Assert.Fail(
            $"SSE role hydration failed: cache.GetRole({steamId})={cache.GetRole(steamId) ?? "<null>"} " +
            "(expected 'sse-test-role')");
    }
}
