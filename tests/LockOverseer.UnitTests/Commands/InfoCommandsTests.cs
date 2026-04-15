using LockOverseer.Commands;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class InfoCommandsTests
{
    [Fact]
    public async Task Whois_dms_caller_full_record()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.info").Returns(true);
        svc.GetPlayerAsync(76561198000000999).Returns(new PlayerRecord(
            76561198000000999, "Alice",
            DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-04-15T10:00:00Z"),
            TotalPlaytimeSeconds: 7200,
            CurrentRole: "mod",
            Flags: new[] { "overseer.ban" },
            ActiveBan: null,
            ActiveMute: null));

        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new InfoCommands(svc, gate, resolver, (_, m) => dms.Add(m), statusProvider: () => "stub");

        await cmds.HandleWhoisAsync(100, new[] { "76561198000000999" });

        dms.ShouldContain(m => m.Contains("Alice") && m.Contains("mod") && m.Contains("7200"));
    }
}
