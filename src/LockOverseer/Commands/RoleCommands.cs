using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;

namespace LockOverseer.Commands;

public sealed class RoleCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;

    public RoleCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver, Action<long, string> dm)
    { _svc = svc; _gate = gate; _resolver = resolver; _dm = dm; }

    [RequireFlag("overseer.role")]
    public async Task HandleGrantAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.role")) return;
        if (args.Count < 2) { _dm(callerSteamId, "Usage: /role grant <player> <role> [minutes|perm]"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }

        int? minutes = null;
        if (args.Count >= 3 && !DurationParser.TryParse(args[2], out minutes))
        { _dm(callerSteamId, "Invalid duration"); return; }

        var result = await _svc.GrantRoleAsync(
            new RoleGrantRequest(target.SteamId, args[1], minutes, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? $"Granted {args[1]}." : $"Failed: {result.Error!.Message}");
    }

    [RequireFlag("overseer.role")]
    public async Task HandleRevokeAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.role")) return;
        if (args.Count < 1) { _dm(callerSteamId, "Usage: /role revoke <player>"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }
        if (!_gate.AssertOutranks(callerSteamId, target.SteamId)) return;

        var id = await _svc.GetActiveRoleAssignmentIdAsync(target.SteamId).ConfigureAwait(false);
        if (id is null) { _dm(callerSteamId, "No active role to revoke"); return; }

        var result = await _svc.RevokeRoleAsync(id.Value,
            new RevokeRequest(null, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? "Role revoked." : $"Failed: {result.Error!.Message}");
    }
}
