using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Services;

public sealed class LockOverseerServiceHelpersTests
{
    private static (LockOverseerService svc, AuthorityCache cache, IAuthorityClient client) Build()
    {
        var client = Substitute.For<IAuthorityClient>();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AuthorityCache(time, Options.Create(new LockOverseerConfig()), NullLogger<AuthorityCache>.Instance);
        var svc = new LockOverseerService(client, cache, NullLogger<LockOverseerService>.Instance);
        return (svc, cache, client);
    }

    [Fact]
    public void GetRolePriority_returns_zero_when_unknown()
    {
        var (svc, _, _) = Build();
        svc.GetRolePriority(76561198000000001L).ShouldBe(0);
    }

    [Fact]
    public async Task GetActiveBanIdAsync_reads_from_cache_when_present()
    {
        var (svc, cache, _) = Build();
        var ban = new Ban(42L, 76561198000000001L, "x",
            DateTimeOffset.UtcNow, null, null,
            new Issuer(null, "test"), null);
        cache.UpsertActiveBan(ban);

        var id = await svc.GetActiveBanIdAsync(76561198000000001L);

        id.ShouldBe(42L);
    }

    [Fact]
    public async Task GetActiveBanIdAsync_returns_null_when_no_active_ban()
    {
        var (svc, _, _) = Build();
        (await svc.GetActiveBanIdAsync(76561198000000002L)).ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveMuteIdAsync_returns_cached_id_or_null()
    {
        var (svc, cache, _) = Build();
        var mute = new Mute(77L, 76561198000000003L, null,
            DateTimeOffset.UtcNow, null, null,
            new Issuer(null, "test"), null);
        cache.UpsertActiveMute(mute);

        (await svc.GetActiveMuteIdAsync(76561198000000003L)).ShouldBe(77L);
        (await svc.GetActiveMuteIdAsync(76561198000000004L)).ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveRoleAssignmentIdAsync_asks_authority_client()
    {
        var (svc, _, client) = Build();
        client.GetActiveRoleAssignmentAsync(76561198000000005L, Arg.Any<CancellationToken>())
              .Returns(Result<RoleAssignment>.Ok(new RoleAssignment(
                  101L, 76561198000000005L, "mod", DateTimeOffset.UtcNow, null, null,
                  new Issuer(null, "test"))));

        (await svc.GetActiveRoleAssignmentIdAsync(76561198000000005L)).ShouldBe(101L);
    }

    [Fact]
    public async Task GetActiveFlagAssignmentIdAsync_asks_authority_client_for_flag()
    {
        var (svc, _, client) = Build();
        client.GetActiveFlagAssignmentAsync(76561198000000006L, "locktimer.can_reset_map", Arg.Any<CancellationToken>())
              .Returns(Result<FlagAssignment>.Ok(new FlagAssignment(
                  202L, 76561198000000006L, "locktimer.can_reset_map",
                  DateTimeOffset.UtcNow, null, null, new Issuer(null, "test"))));

        (await svc.GetActiveFlagAssignmentIdAsync(76561198000000006L, "locktimer.can_reset_map"))
            .ShouldBe(202L);
    }

    [Fact]
    public async Task GetAuditAsync_forwards_paging_to_authority_client()
    {
        var (svc, _, client) = Build();
        var page = new List<AuditEntry>
        {
            new(1, DateTimeOffset.UtcNow, "ban.issue", 1, 2, "chat", "{}")
        };
        client.GetAuditAsync(2, 25, Arg.Any<CancellationToken>())
              .Returns(Result<IReadOnlyList<AuditEntry>>.Ok(page));

        var result = await svc.GetAuditAsync(2, 25);

        result.Count.ShouldBe(1);
        result[0].Action.ShouldBe("ban.issue");
    }
}
