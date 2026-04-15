using LockOverseer.Commands;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class BanCommandsTests
{
    [Fact]
    public async Task Ban_issues_via_service_with_actor_and_reason()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.ban").Returns(true);
        svc.GetRolePriority(100).Returns(100);
        svc.GetRolePriority(76561198000000999).Returns(0);
        svc.IssueBanAsync(Arg.Any<BanRequest>()).Returns(Result<Ban>.Ok(
            new Ban(1, 76561198000000999, "griefing", DateTimeOffset.UtcNow, null, null,
                    new Issuer(100, "chat"), null)));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, msg) => dms.Add(msg));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new BanCommands(svc, gate, resolver, (_, msg) => dms.Add(msg));

        await cmds.HandleBanAsync(callerSteamId: 100,
                                   args: new[] { "76561198000000999", "perm", "griefing" });

        await svc.Received(1).IssueBanAsync(Arg.Is<BanRequest>(r =>
            r.SteamId == 76561198000000999L &&
            r.DurationMinutes == null &&
            r.Reason == "griefing" &&
            r.IssuedBy.SteamId == 100 &&
            r.IssuedBy.Label == "chat"));
    }

    [Fact]
    public async Task Unban_looks_up_active_ban_and_revokes()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.ban").Returns(true);
        svc.GetRolePriority(100).Returns(100);
        svc.GetRolePriority(76561198000000999).Returns(0);
        svc.GetActiveBanIdAsync(76561198000000999).Returns(ValueTask.FromResult<long?>(77));
        svc.RevokeBanAsync(77, Arg.Any<RevokeRequest>()).Returns(Result<Ban>.Ok(
            new Ban(77, 76561198000000999, null, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow,
                    new Issuer(100, "chat"), new Issuer(100, "chat"))));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new BanCommands(svc, gate, resolver, (_, m) => dms.Add(m));

        await cmds.HandleUnbanAsync(100, new[] { "76561198000000999", "appeal", "accepted" });

        await svc.Received(1).RevokeBanAsync(77, Arg.Is<RevokeRequest>(r =>
            r.Reason == "appeal accepted" && r.RevokedBy.Label == "chat"));
    }
}
