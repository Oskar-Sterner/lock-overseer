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
             "issued_by":{"steam_id":null,"label":"chat"},"revoked_by":null}
            """;
        var r = JsonSerializer.Deserialize<BanResource>(json, JsonDefaults.Options)!;
        r.Id.ShouldBe(5);
        r.SteamId.ShouldBe(76561198000000001);
        r.IssuedBy.Label.ShouldBe("chat");
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
