using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;

namespace LockOverseer.Commands;

public sealed class BanCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;

    public BanCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver, Action<long, string> dm)
    {
        _svc = svc;
        _gate = gate;
        _resolver = resolver;
        _dm = dm;
    }

    [RequireFlag("overseer.ban")]
    public async Task HandleBanAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.ban")) return;
        if (args.Count < 2)
        {
            _dm(callerSteamId, "Usage: /ban <player> <minutes|perm> [reason...]");
            return;
        }

        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved)
        {
            _dm(callerSteamId, target.Kind == ResolverResultKind.Ambiguous
                ? $"Ambiguous match: {string.Join(", ", target.Matches.Select(m => m.Name))}"
                : "Player not found (offline players require Steam64)");
            return;
        }

        if (!_gate.AssertOutranks(callerSteamId, target.SteamId)) return;

        if (!DurationParser.TryParse(args[1], out var minutes))
        {
            _dm(callerSteamId, "Invalid duration (use minutes or `perm`)");
            return;
        }

        var reason = ReasonParser.JoinReason(args.Skip(2).ToArray());
        var req = new BanRequest(target.SteamId, minutes, reason, new Issuer(callerSteamId, "chat"));
        var result = await _svc.IssueBanAsync(req).ConfigureAwait(false);

        _dm(callerSteamId, result.IsSuccess
            ? $"Banned {target.SteamId} ({(minutes is null ? "perm" : minutes + "m")})"
            : $"Ban failed: {result.Error!.Message}");
    }
}
