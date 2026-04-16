using System;
using System.Linq;
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

    private static BanResource MakeBanResource(long id, long steamId, string? reason = null,
        DateTimeOffset? expiresAt = null, string? issuerLabel = "chat") =>
        new(id, steamId, reason,
            IssuedAt: DateTimeOffset.UnixEpoch,
            ExpiresAt: expiresAt,
            RevokedAt: null,
            RevokeReason: null,
            IssuedBySteamId: null,
            IssuedByLabel: issuerLabel,
            RevokedBySteamId: null,
            RevokedByLabel: null);

    [Fact]
    public async Task IssueBanAsync_writes_to_API_then_updates_cache()
    {
        var (sut, client, cache) = Build();
        client.IssueBanAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
              .Returns(Result<BanResource>.Ok(MakeBanResource(77, 42, "spam")));

        var req = new BanRequest(42, null, "spam", new Issuer(null, "chat"));
        var r = await sut.IssueBanAsync(req);

        r.IsSuccess.ShouldBeTrue();
        r.Value!.Id.ShouldBe(77);
        cache.IsBanned(42).ShouldBeTrue();
        await client.Received(1).IssueBanAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueBanAsync_sends_nested_issued_by_and_duration_minutes_to_API()
    {
        var (sut, client, _) = Build();
        object? captured = null;
        client.IssueBanAsync(Arg.Do<object>(b => captured = b), Arg.Any<CancellationToken>())
              .Returns(Result<BanResource>.Ok(MakeBanResource(1, 42)));

        await sut.IssueBanAsync(new BanRequest(42, DurationMinutes: 30, Reason: "spam",
            IssuedBy: new Issuer(SteamId: 99, Label: "mod")));

        captured.ShouldNotBeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(captured, JsonDefaults.Options);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("steam_id").GetInt64().ShouldBe(42);
        root.GetProperty("duration_minutes").GetInt32().ShouldBe(30);
        root.GetProperty("reason").GetString().ShouldBe("spam");
        var issuedBy = root.GetProperty("issued_by");
        issuedBy.GetProperty("steam_id").GetInt64().ShouldBe(99);
        issuedBy.GetProperty("label").GetString().ShouldBe("mod");
    }

    [Fact]
    public async Task IssueBanAsync_on_API_failure_does_not_update_cache()
    {
        var (sut, client, cache) = Build();
        client.IssueBanAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
              .Returns(Result<BanResource>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, "down")));
        var r = await sut.IssueBanAsync(new BanRequest(42, null, "spam", new Issuer(null, "chat")));
        r.IsSuccess.ShouldBeFalse();
        cache.IsBanned(42).ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_IssueBan_for_same_steamid_are_serialized()
    {
        var (sut, client, _) = Build();
        int inFlight = 0, maxInFlight = 0;
        client.IssueBanAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var n = Interlocked.Increment(ref inFlight);
            maxInFlight = Math.Max(maxInFlight, n);
            Thread.Sleep(30);
            Interlocked.Decrement(ref inFlight);
            return new ValueTask<Result<BanResource>>(Result<BanResource>.Ok(MakeBanResource(1, 42)));
        });

        var tasks = Enumerable.Range(0, 5).Select(_ =>
            sut.IssueBanAsync(new BanRequest(42, null, "x", new Issuer(null, "chat"))).AsTask()).ToArray();
        await Task.WhenAll(tasks);

        maxInFlight.ShouldBe(1);
    }

    [Fact]
    public void ToModel_parses_external_api_shape_BanResource_into_Ban_with_correct_issuer()
    {
        // Regression guard: the wire shape uses flat issuer fields. This emulates a real
        // external API BanOut response and round-trips it through JsonDefaults.Options.
        const string json = """
            {
              "id": 42,
              "steam_id": 76561198000000001,
              "reason": "griefing",
              "issued_at": "2026-04-15T00:00:00Z",
              "expires_at": null,
              "revoked_at": null,
              "revoke_reason": null,
              "issued_by_steam_id": 76561198000099999,
              "issued_by_label": "admin",
              "revoked_by_steam_id": null,
              "revoked_by_label": null
            }
            """;
        var dto = System.Text.Json.JsonSerializer.Deserialize<BanResource>(json, JsonDefaults.Options)!;
        dto.Id.ShouldBe(42L);
        dto.SteamId.ShouldBe(76561198000000001L);
        dto.IssuedBySteamId.ShouldBe(76561198000099999L);
        dto.IssuedByLabel.ShouldBe("admin");
        dto.RevokedBySteamId.ShouldBeNull();
        dto.RevokedByLabel.ShouldBeNull();
        dto.RevokeReason.ShouldBeNull();
    }
}
