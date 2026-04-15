using System;
using System.Collections.Generic;
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
                  new BanResource(1, 42, null, DateTimeOffset.UnixEpoch, null, null,
                      new IssuerResource(null, "sys"), null)
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
}
