using System;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using LockOverseer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Services;

public sealed class LockOverseerServiceTests
{
    private static (LockOverseerService Sut, IAuthorityClient Client, AuthorityCache Cache) Build()
    {
        var client = Substitute.For<IAuthorityClient>();
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AuthorityCache(time, Options.Create(new LockOverseerConfig()), NullLogger<AuthorityCache>.Instance);
        var sut = new LockOverseerService(client, cache, NullLogger<LockOverseerService>.Instance);
        return (sut, client, cache);
    }

    [Fact]
    public async Task IssueBanAsync_writes_to_API_then_updates_cache()
    {
        var (sut, client, cache) = Build();
        client.IssueBanAsync(Arg.Any<BanResource>(), Arg.Any<CancellationToken>())
              .Returns(Result<BanResource>.Ok(
                  new BanResource(77, 42, "spam",
                      DateTimeOffset.UnixEpoch, null, null,
                      new IssuerResource(null, "chat"), null)));

        var req = new BanRequest(42, null, "spam", new Issuer(null, "chat"));
        var r = await sut.IssueBanAsync(req);

        r.IsSuccess.ShouldBeTrue();
        r.Value!.Id.ShouldBe(77);
        cache.IsBanned(42).ShouldBeTrue();
        await client.Received(1).IssueBanAsync(Arg.Any<BanResource>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueBanAsync_on_API_failure_does_not_update_cache()
    {
        var (sut, client, cache) = Build();
        client.IssueBanAsync(Arg.Any<BanResource>(), Arg.Any<CancellationToken>())
              .Returns(Result<BanResource>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, "down")));
        var r = await sut.IssueBanAsync(new BanRequest(42, null, "spam", new Issuer(null, "chat")));
        r.IsSuccess.ShouldBeFalse();
        cache.IsBanned(42).ShouldBeFalse();
    }
}
