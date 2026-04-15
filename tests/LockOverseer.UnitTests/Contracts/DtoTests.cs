using System;
using System.Collections.Generic;
using LockOverseer.Contracts.Models;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Contracts;

public sealed class DtoTests
{
    [Fact]
    public void PlayerRecord_can_be_constructed_with_all_fields()
    {
        var now = DateTimeOffset.UnixEpoch;
        var ban = new Ban(1, 76561198000000001, "spam", now, null, null,
            new Issuer(null, "system"), null);
        var rec = new PlayerRecord(
            SteamId: 76561198000000001,
            LastKnownName: "alice",
            FirstConnectAt: now,
            LastConnectAt: now,
            TotalPlaytimeSeconds: 120,
            CurrentRole: "admin",
            Flags: new List<string> { "overseer.ban" },
            ActiveBan: ban,
            ActiveMute: null);

        rec.SteamId.ShouldBe(76561198000000001);
        rec.ActiveBan!.Reason.ShouldBe("spam");
        rec.Flags.ShouldContain("overseer.ban");
    }

    [Fact]
    public void Issuer_supports_system_label_without_steamid()
    {
        var i = new Issuer(null, "chat");
        i.SteamId.ShouldBeNull();
        i.Label.ShouldBe("chat");
    }
}
