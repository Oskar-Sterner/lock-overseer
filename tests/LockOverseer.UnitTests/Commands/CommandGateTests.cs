using LockOverseer.Commands;
using LockOverseer.Contracts;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class CommandGateTests
{
    [Fact]
    public void Missing_flag_denies_and_sends_dm()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(42, "overseer.ban").Returns(false);

        var dms = new List<string>();
        var gate = new CommandGate(svc, (steamId, msg) => dms.Add($"{steamId}:{msg}"));

        var ok = gate.RequireFlag(callerSteamId: 42, flag: "overseer.ban");

        ok.ShouldBeFalse();
        dms.ShouldContain("42:Permission denied (requires `overseer.ban`)");
    }

    [Fact]
    public void Present_flag_allows_and_sends_nothing()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(42, "overseer.ban").Returns(true);
        var dms = new List<string>();
        var gate = new CommandGate(svc, (steamId, msg) => dms.Add(msg));

        gate.RequireFlag(42, "overseer.ban").ShouldBeTrue();
        dms.ShouldBeEmpty();
    }
}
