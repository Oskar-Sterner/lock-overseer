using System;
using System.Text.Json;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class DtoRoundTripTests
{
    [Fact]
    public void BanResource_roundtrips_snake_case_with_utc_timestamps()
    {
        const string json = """
            {"id":5,"steam_id":76561198000000001,"reason":"spam",
             "issued_at":"2026-04-15T12:00:00Z","expires_at":null,"revoked_at":null,
             "revoke_reason":null,
             "issued_by_steam_id":null,"issued_by_label":"chat",
             "revoked_by_steam_id":null,"revoked_by_label":null}
            """;
        var r = JsonSerializer.Deserialize<BanResource>(json, JsonDefaults.Options)!;
        r.Id.ShouldBe(5);
        r.SteamId.ShouldBe(76561198000000001);
        r.IssuedByLabel.ShouldBe("chat");
        r.IssuedBySteamId.ShouldBeNull();
        r.RevokedByLabel.ShouldBeNull();
        r.IssuedAt.ShouldBe(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ProblemDetails_roundtrips()
    {
        const string json = """{"type":"about:blank","title":"Conflict","status":409,"detail":"ban exists"}""";
        var p = JsonSerializer.Deserialize<ProblemDetails>(json, JsonDefaults.Options)!;
        p.Status.ShouldBe(409);
        p.Detail.ShouldBe("ban exists");
    }
}
