using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;

namespace LockOverseer.Commands;

public sealed class FlagCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;

    public FlagCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver, Action<long, string> dm)
    { _svc = svc; _gate = gate; _resolver = resolver; _dm = dm; }

    [RequireFlag("overseer.flag")]
    public async Task HandleGrantAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.flag")) return;
        if (args.Count < 2) { _dm(callerSteamId, "Usage: /flag grant <player> <flag> [minutes|perm]"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }

        int? minutes = null;
        if (args.Count >= 3 && !DurationParser.TryParse(args[2], out minutes))
        { _dm(callerSteamId, "Invalid duration"); return; }

        var result = await _svc.GrantFlagAsync(
            new FlagGrantRequest(target.SteamId, args[1], minutes, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? $"Granted flag {args[1]}." : $"Failed: {result.Error!.Message}");
    }

    [RequireFlag("overseer.flag")]
    public async Task HandleRevokeAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.flag")) return;
        if (args.Count < 2) { _dm(callerSteamId, "Usage: /flag revoke <player> <flag>"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }

        var id = await _svc.GetActiveFlagAssignmentIdAsync(target.SteamId, args[1]).ConfigureAwait(false);
        if (id is null) { _dm(callerSteamId, "No active flag assignment"); return; }

        var result = await _svc.RevokeFlagAsync(id.Value,
            new RevokeRequest(null, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? "Flag revoked." : $"Failed: {result.Error!.Message}");
    }
}
