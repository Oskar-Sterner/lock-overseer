using LockOverseer.Commands;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class FlagCommandsTests
{
    [Fact]
    public async Task Grant_flag_forwards_flag_name()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.flag").Returns(true);
        svc.GrantFlagAsync(Arg.Any<FlagGrantRequest>()).Returns(Result<FlagAssignment>.Ok(
            new FlagAssignment(1, 76561198000000999, "locktimer.can_reset_map",
                DateTimeOffset.UtcNow, null, null, new Issuer(100, "chat"))));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new FlagCommands(svc, gate, resolver, (_, m) => dms.Add(m));

        await cmds.HandleGrantAsync(100, new[] { "76561198000000999", "locktimer.can_reset_map" });

        await svc.Received(1).GrantFlagAsync(Arg.Is<FlagGrantRequest>(r =>
            r.Flag == "locktimer.can_reset_map"));
    }
}
