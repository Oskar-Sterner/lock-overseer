using LockOverseer.Commands;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class RoleCommandsTests
{
    [Fact]
    public async Task Grant_forwards_role_name_and_duration()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.role").Returns(true);
        svc.GetRolePriority(100).Returns(100);
        svc.GetRolePriority(76561198000000999).Returns(0);
        svc.GrantRoleAsync(Arg.Any<RoleGrantRequest>()).Returns(Result<RoleAssignment>.Ok(
            new RoleAssignment(1, 76561198000000999, "mod", DateTimeOffset.UtcNow, null, null,
                               new Issuer(100, "chat"))));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new RoleCommands(svc, gate, resolver, (_, m) => dms.Add(m));

        await cmds.HandleGrantAsync(100, new[] { "76561198000000999", "mod", "perm" });

        await svc.Received(1).GrantRoleAsync(Arg.Is<RoleGrantRequest>(r =>
            r.RoleName == "mod" && r.DurationMinutes == null));
    }
}
