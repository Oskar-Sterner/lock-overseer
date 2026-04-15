using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Caching;

public sealed class ReconcileServiceTests
{
    [Fact]
    public async Task Initial_reconcile_hydrates_cache_from_AuthorityClient()
    {
        var client = Substitute.For<IAuthorityClient>();
        client.GetActiveBansAsync(Arg.Any<CancellationToken>())
              .Returns(Result<IReadOnlyList<BanResource>>.Ok(new[]
              {
                  new BanResource(1, 42, null,
                      IssuedAt: DateTimeOffset.UnixEpoch,
                      ExpiresAt: null,
                      RevokedAt: null,
                      RevokeReason: null,
                      IssuedBySteamId: null,
                      IssuedByLabel: "sys",
                      RevokedBySteamId: null,
                      RevokedByLabel: null)
              }));
        client.GetActiveMutesAsync(Arg.Any<CancellationToken>())
              .Returns(Result<IReadOnlyList<MuteResource>>.Ok(Array.Empty<MuteResource>()));
        client.GetRolesAsync(Arg.Any<CancellationToken>())
              .Returns(Result<IReadOnlyList<RoleResource>>.Ok(Array.Empty<RoleResource>()));

        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AuthorityCache(time, Options.Create(new LockOverseerConfig()), NullLogger<AuthorityCache>.Instance);
        var svc = new ReconcileService(client, cache, Options.Create(new LockOverseerConfig()), NullLogger<ReconcileService>.Instance, time);

        await svc.ReconcileOnceAsync(CancellationToken.None);
        cache.IsBanned(42).ShouldBeTrue();
    }

    [Fact]
    public async Task Emits_one_warn_on_enter_degraded_and_one_info_on_recovery()
    {
        var client = Substitute.For<IAuthorityClient>();
        client.GetActiveBansAsync(Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<BanResource>>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, "down")),
            Result<IReadOnlyList<BanResource>>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, "down")),
            Result<IReadOnlyList<BanResource>>.Ok(Array.Empty<BanResource>()));
        client.GetActiveMutesAsync(Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<MuteResource>>.Ok(Array.Empty<MuteResource>()));
        client.GetRolesAsync(Arg.Any<CancellationToken>()).Returns(
            Result<IReadOnlyList<RoleResource>>.Ok(Array.Empty<RoleResource>()));

        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ReconcileService>>();
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AuthorityCache(time, Options.Create(new LockOverseerConfig()), NullLogger<AuthorityCache>.Instance);
        var svc = new ReconcileService(client, cache, Options.Create(new LockOverseerConfig()), logger, time);

        await svc.ReconcileOnceAsync(CancellationToken.None); // warn
        await svc.ReconcileOnceAsync(CancellationToken.None); // silent (still degraded)
        await svc.ReconcileOnceAsync(CancellationToken.None); // info

        logger.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == "Log" &&
                        (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == Microsoft.Extensions.Logging.LogLevel.Warning)
            .ShouldBe(1);
        logger.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == "Log" &&
                        (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == Microsoft.Extensions.Logging.LogLevel.Information)
            .ShouldBe(1);
    }
}
