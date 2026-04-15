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
}
