using LockOverseer.Commands;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class MuteCommandsTests
{
    [Fact]
    public async Task Mute_passes_minutes_and_reason()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.mute").Returns(true);
        svc.GetRolePriority(100).Returns(100);
        svc.GetRolePriority(76561198000000999).Returns(0);
        svc.IssueMuteAsync(Arg.Any<MuteRequest>()).Returns(Result<Mute>.Ok(
            new Mute(1, 76561198000000999, "toxic", DateTimeOffset.UtcNow, null, null,
                     new Issuer(100, "chat"), null)));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new MuteCommands(svc, gate, resolver, (_, m) => dms.Add(m));

        await cmds.HandleMuteAsync(100, new[] { "76561198000000999", "30", "toxic" });

        await svc.Received(1).IssueMuteAsync(Arg.Is<MuteRequest>(r =>
            r.DurationMinutes == 30 && r.Reason == "toxic"));
    }
}
