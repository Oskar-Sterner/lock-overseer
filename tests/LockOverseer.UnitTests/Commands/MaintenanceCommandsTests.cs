using LockOverseer.Commands;
using LockOverseer.Contracts;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class MaintenanceCommandsTests
{
    [Fact]
    public async Task Reload_triggers_reconcile()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.admin").Returns(true);
        var kicks = new List<(long SteamId, string Reason)>();
        var reconciled = false;
        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new MaintenanceCommands(svc, gate, resolver, (_, m) => dms.Add(m),
                                            (s, r) => kicks.Add((s, r)), () => { reconciled = true; return Task.CompletedTask; });

        await cmds.HandleReloadAsync(100);

        reconciled.ShouldBeTrue();
    }

    [Fact]
    public async Task Kick_invokes_kick_effect_with_reason()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.kick").Returns(true);
        svc.GetRolePriority(100).Returns(100);
        svc.GetRolePriority(76561198000000999).Returns(0);
        var kicks = new List<(long, string)>();
        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => new[] { new ResolverCandidate(76561198000000999, 3, "Alice") });
        var cmds = new MaintenanceCommands(svc, gate, resolver, (_, m) => dms.Add(m),
                                            (steamId, reason) => kicks.Add((steamId, reason)),
                                            () => Task.CompletedTask);

        await cmds.HandleKickAsync(100, new[] { "#3", "afk", "too", "long" });

        kicks.ShouldContain((76561198000000999, "afk too long"));
    }
}
