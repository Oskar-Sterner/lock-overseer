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

    [Fact]
    public void Help_lists_all_commands()
    {
        var svc = Substitute.For<ILockOverseerService>();
        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var cmds = new InfoCommands(svc, gate, resolver, (_, m) => dms.Add(m), () => "stub");

        cmds.HandleHelp(100);

        var text = string.Join('\n', dms);
        text.ShouldContain("/ban");
        text.ShouldContain("/mute");
        text.ShouldContain("/role grant");
        text.ShouldContain("/flag grant");
        text.ShouldContain("/overseer status");
    }

    [Fact]
    public void Status_prints_exact_spec_format()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.HasFlag(100, "overseer.admin").Returns(true);
        var dms = new List<string>();
        var gate = new CommandGate(svc, (_, m) => dms.Add(m));
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());
        var status =
            "Authority API:   ok\n" +
            "Last reconcile:  2026-04-15T18:32:11Z (3m 12s ago)\n" +
            "Cache:           4172 bans · 1203 mutes · 24 connected\n" +
            "Outbox pending:  0\n" +
            "HTTP listener:   127.0.0.1:27080 (enabled)";
        var cmds = new InfoCommands(svc, gate, resolver, (_, m) => dms.Add(m), () => status);

        cmds.HandleStatus(100);

        dms.ShouldContain(m => m.Contains("Authority API:"));
        dms.ShouldContain(m => m.Contains("Last reconcile:"));
        dms.ShouldContain(m => m.Contains("HTTP listener:"));
    }
}
