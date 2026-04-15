using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Caching;

public sealed class AuthorityCacheTests
{
    private static AuthorityCache Build(FakeTimeProvider? time = null, LockOverseerConfig? cfg = null)
    {
        return new AuthorityCache(
            time ?? new FakeTimeProvider(DateTimeOffset.Parse("2026-04-15T00:00:00Z")),
            Options.Create(cfg ?? new LockOverseerConfig()),
            NullLogger<AuthorityCache>.Instance);
    }

    [Fact]
    public void IsBanned_returns_false_for_unknown_steamid()
    {
        var c = Build();
        c.IsBanned(42).ShouldBeFalse();
    }

    [Fact]
    public void ReplaceActiveBans_then_IsBanned_returns_true()
    {
        var c = Build();
        c.ReplaceActiveBans(new[]
        {
            new Ban(1, 42, null, DateTimeOffset.UnixEpoch, null, null, new Issuer(null,"sys"), null)
        });
        c.IsBanned(42).ShouldBeTrue();
    }

    [Fact]
    public void IsBanned_returns_false_once_fake_clock_passes_expires_at()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-15T00:00:00Z"));
        var c = Build(time);
        c.UpsertActiveBan(new Ban(1, 42, null,
            IssuedAt: time.GetUtcNow(),
            ExpiresAt: time.GetUtcNow().AddMinutes(5),
            RevokedAt: null,
            IssuedBy: new Issuer(null, "sys"), RevokedBy: null));
        c.IsBanned(42).ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(6));
        c.IsBanned(42).ShouldBeFalse();
    }

    [Fact]
    public void SweepExpired_removes_revoked_and_expired_rows()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-15T00:00:00Z"));
        var c = Build(time);
        c.UpsertActiveBan(new Ban(1, 1, null, time.GetUtcNow(), time.GetUtcNow().AddMinutes(1), null, new Issuer(null, "sys"), null));
        c.UpsertActiveBan(new Ban(2, 2, null, time.GetUtcNow(), null, time.GetUtcNow(), new Issuer(null, "sys"), null));
        c.UpsertActiveBan(new Ban(3, 3, null, time.GetUtcNow(), null, null, new Issuer(null, "sys"), null));

        time.Advance(TimeSpan.FromMinutes(2));
        c.SweepExpired().ShouldBe(2);
        c.IsBanned(1).ShouldBeFalse();
        c.IsBanned(2).ShouldBeFalse();
        c.IsBanned(3).ShouldBeTrue();
    }

    [Fact]
    public void ReplaceActiveBans_truncates_when_ceiling_exceeded()
    {
        var cfg = new LockOverseerConfig();
        cfg.Cache.MaxActiveBans = 2;
        var c = Build(cfg: cfg);
        c.ReplaceActiveBans(new[]
        {
            new Ban(1,1,null,DateTimeOffset.UnixEpoch,null,null,new Issuer(null,"sys"),null),
            new Ban(2,2,null,DateTimeOffset.UnixEpoch,null,null,new Issuer(null,"sys"),null),
            new Ban(3,3,null,DateTimeOffset.UnixEpoch,null,null,new Issuer(null,"sys"),null),
        });
        c.SnapshotActiveBans().Count.ShouldBe(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_reads_do_not_throw_during_mutation()
    {
        var c = Build();
        using var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromMilliseconds(300));
        var reader = System.Threading.Tasks.Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
                _ = c.IsBanned(1);
        });
        var writer = System.Threading.Tasks.Task.Run(() =>
        {
            long i = 0;
            while (!cts.IsCancellationRequested)
            {
                c.UpsertActiveBan(new Ban(i, 1, null, DateTimeOffset.UnixEpoch, null, null, new Issuer(null, "sys"), null));
                c.RemoveActiveBan(1);
                i++;
            }
        });
        await System.Threading.Tasks.Task.WhenAll(reader, writer);
        true.ShouldBeTrue();
    }
}
