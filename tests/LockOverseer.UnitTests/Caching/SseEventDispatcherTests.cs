using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Caching;

public sealed class SseEventDispatcherTests
{
    private static (SseEventDispatcher d, AuthorityCache cache,
                    IPlayerKicker kicker, ILockOverseerService svc,
                    ReconcileService reconcile) Build()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(System.DateTimeOffset.UtcNow);
        var cache = new AuthorityCache(time, Options.Create(new LockOverseerConfig()),
                                       NullLogger<AuthorityCache>.Instance);
        var kicker = Substitute.For<IPlayerKicker>();
        var svc = Substitute.For<ILockOverseerService>();
        var client = Substitute.For<IAuthorityClient>();
        var reconcile = new ReconcileService(client, cache,
            Options.Create(new LockOverseerConfig()),
            NullLogger<ReconcileService>.Instance, time);
        var d = new SseEventDispatcher(cache, kicker, svc, reconcile,
                                       NullLogger<SseEventDispatcher>.Instance);
        return (d, cache, kicker, svc, reconcile);
    }

    [Fact]
    public async Task BanCreated_upserts_and_kicks()
    {
        var (d, cache, kicker, _, _) = Build();
        await d.DispatchAsync(new SseFrame(
            Id: 1, Event: "ban.created",
            Data: "{\"ban_id\":17,\"steam_id\":42,\"reason\":\"x\"," +
                  "\"issued_at\":\"2026-04-16T10:00:00+00:00\"," +
                  "\"expires_at\":null,\"issued_by_steam_id\":null," +
                  "\"issued_by_label\":\"sys\"}"),
            CancellationToken.None);
        cache.IsBanned(42).ShouldBeTrue();
        kicker.Received(1).KickBySteamId(42, "x");
    }

    [Fact]
    public async Task BanRevoked_removes_and_does_not_kick()
    {
        var (d, cache, kicker, _, _) = Build();
        cache.UpsertActiveBan(new LockOverseer.Contracts.Models.Ban(
            9, 42, "x", System.DateTimeOffset.UtcNow, null, null,
            new LockOverseer.Contracts.Models.Issuer(null, "sys"), null));
        cache.IsBanned(42).ShouldBeTrue();
        await d.DispatchAsync(new SseFrame(
            Id: 2, Event: "ban.revoked",
            Data: "{\"ban_id\":9,\"steam_id\":42,\"revoked_at\":\"2026-04-16T10:00:00+00:00\",\"revoke_reason\":null}"),
            CancellationToken.None);
        cache.IsBanned(42).ShouldBeFalse();
        kicker.DidNotReceive().KickBySteamId(Arg.Any<long>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RoleAssignmentCreated_for_connected_player_rehydrates()
    {
        var (d, cache, _, svc, _) = Build();
        cache.SetConnectedState(99, new ConnectedPlayerState(null, FrozenSet<string>.Empty));
        await d.DispatchAsync(new SseFrame(
            Id: 3, Event: "role_assignment.created",
            Data: "{\"assignment_id\":1,\"steam_id\":99,\"role_name\":\"admin\",\"expires_at\":null}"),
            CancellationToken.None);
        await svc.Received(1).HydrateConnectedAsync(99, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleAssignmentCreated_for_offline_player_is_dropped()
    {
        var (d, _, _, svc, _) = Build();
        await d.DispatchAsync(new SseFrame(
            Id: 4, Event: "role_assignment.created",
            Data: "{\"assignment_id\":1,\"steam_id\":555,\"role_name\":\"admin\",\"expires_at\":null}"),
            CancellationToken.None);
        await svc.DidNotReceive().HydrateConnectedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncRequired_updates_last_event_id()
    {
        var (d, _, _, _, _) = Build();
        await d.DispatchAsync(new SseFrame(
            Id: 5, Event: "sync_required", Data: "{\"latest_id\":100}"),
            CancellationToken.None);
        d.LastEventId.ShouldBe(100L);
    }

    [Fact]
    public async Task Unknown_event_is_ignored()
    {
        var (d, cache, kicker, svc, _) = Build();
        await d.DispatchAsync(new SseFrame(
            Id: 6, Event: "future.feature", Data: "{}"),
            CancellationToken.None);
        cache.ConnectedCount.ShouldBe(0);
        kicker.DidNotReceive().KickBySteamId(Arg.Any<long>(), Arg.Any<string>());
        await svc.DidNotReceive().HydrateConnectedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MuteCreated_upserts_and_does_not_kick()
    {
        var (d, cache, kicker, _, _) = Build();
        await d.DispatchAsync(new SseFrame(
            Id: 7, Event: "mute.created",
            Data: "{\"mute_id\":1,\"steam_id\":77,\"reason\":\"spam\"," +
                  "\"issued_at\":\"2026-04-16T10:00:00+00:00\"," +
                  "\"expires_at\":null}"),
            CancellationToken.None);
        cache.IsMuted(77).ShouldBeTrue();
        kicker.DidNotReceive().KickBySteamId(Arg.Any<long>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BanCreated_applied_twice_is_idempotent()
    {
        var (d, cache, kicker, _, _) = Build();
        var frame = new SseFrame(
            Id: 10, Event: "ban.created",
            Data: "{\"ban_id\":17,\"steam_id\":123,\"reason\":\"x\"," +
                  "\"issued_at\":\"2026-04-16T10:00:00+00:00\"," +
                  "\"expires_at\":null,\"issued_by_steam_id\":null," +
                  "\"issued_by_label\":\"sys\"}");
        await d.DispatchAsync(frame, CancellationToken.None);
        await d.DispatchAsync(frame, CancellationToken.None);
        cache.IsBanned(123).ShouldBeTrue();
        // Cache is last-writer-wins keyed by steamId; applying twice is a no-op overwrite.
        // Kicker is called each time — that's fine because KickBySteamId is a no-op
        // for offline players (handled at the plugin seam, out of this unit's scope).
        kicker.Received(2).KickBySteamId(123, "x");
    }
}
