using LockOverseer.Contracts;

namespace LockOverseer.Commands;

public sealed class InfoCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;
    private readonly Func<string> _status;

    public InfoCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver,
                        Action<long, string> dm, Func<string> statusProvider)
    { _svc = svc; _gate = gate; _resolver = resolver; _dm = dm; _status = statusProvider; }

    [RequireFlag("overseer.info")]
    public async Task HandleWhoisAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.info")) return;
        if (args.Count < 1) { _dm(callerSteamId, "Usage: /whois <player>"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }

        var rec = await _svc.GetPlayerAsync(target.SteamId).ConfigureAwait(false);
        if (rec is null) { _dm(callerSteamId, "No record"); return; }

        _dm(callerSteamId, $"[{rec.SteamId}] {rec.LastKnownName ?? "?"} — role={rec.CurrentRole ?? "-"} playtime={rec.TotalPlaytimeSeconds}s");
        _dm(callerSteamId, $"  flags: {(rec.Flags.Count == 0 ? "-" : string.Join(",", rec.Flags))}");
        _dm(callerSteamId, $"  ban: {(rec.ActiveBan is null ? "-" : $"id={rec.ActiveBan.Id} reason={rec.ActiveBan.Reason}")}");
        _dm(callerSteamId, $"  mute: {(rec.ActiveMute is null ? "-" : $"id={rec.ActiveMute.Id} reason={rec.ActiveMute.Reason}")}");
        _dm(callerSteamId, $"  first={rec.FirstConnectAt:o} last={rec.LastConnectAt:o}");
    }

    public void HandleHelp(long callerSteamId)
    {
        _dm(callerSteamId, "LockOverseer commands:");
        _dm(callerSteamId, "  /ban <player> <minutes|perm> [reason]   (requires overseer.ban)");
        _dm(callerSteamId, "  /unban <player> [reason]                (requires overseer.ban)");
        _dm(callerSteamId, "  /mute <player> <minutes|perm> [reason]  (requires overseer.mute)");
        _dm(callerSteamId, "  /unmute <player> [reason]               (requires overseer.mute)");
        _dm(callerSteamId, "  /kick <player> [reason]                 (requires overseer.kick)");
        _dm(callerSteamId, "  /role grant <player> <role> [dur]       (requires overseer.role)");
        _dm(callerSteamId, "  /role revoke <player>                   (requires overseer.role)");
        _dm(callerSteamId, "  /flag grant <player> <flag> [dur]       (requires overseer.flag)");
        _dm(callerSteamId, "  /flag revoke <player> <flag>            (requires overseer.flag)");
        _dm(callerSteamId, "  /whois <player>                         (requires overseer.info)");
        _dm(callerSteamId, "  /overseer status|reload|help            (requires overseer.admin)");
    }
}
