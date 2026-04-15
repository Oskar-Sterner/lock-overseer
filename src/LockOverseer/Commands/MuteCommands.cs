using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;

namespace LockOverseer.Commands;

public sealed class MuteCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;

    public MuteCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver, Action<long, string> dm)
    { _svc = svc; _gate = gate; _resolver = resolver; _dm = dm; }

    [RequireFlag("overseer.mute")]
    public async Task HandleMuteAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.mute")) return;
        if (args.Count < 2) { _dm(callerSteamId, "Usage: /mute <player> <minutes|perm> [reason...]"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }
        if (!_gate.AssertOutranks(callerSteamId, target.SteamId)) return;
        if (!DurationParser.TryParse(args[1], out var minutes))
        { _dm(callerSteamId, "Invalid duration"); return; }

        var reason = ReasonParser.JoinReason(args.Skip(2).ToArray());
        var result = await _svc.IssueMuteAsync(
            new MuteRequest(target.SteamId, minutes, reason, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? "Muted." : $"Mute failed: {result.Error!.Message}");
    }

    [RequireFlag("overseer.mute")]
    public async Task HandleUnmuteAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.mute")) return;
        if (args.Count < 1) { _dm(callerSteamId, "Usage: /unmute <player> [reason...]"); return; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return; }
        if (!_gate.AssertOutranks(callerSteamId, target.SteamId)) return;

        var muteId = await _svc.GetActiveMuteIdAsync(target.SteamId).ConfigureAwait(false);
        if (muteId is null) { _dm(callerSteamId, "No active mute"); return; }

        var reason = ReasonParser.JoinReason(args.Skip(1).ToArray());
        var result = await _svc.RevokeMuteAsync(muteId.Value,
            new RevokeRequest(reason, new Issuer(callerSteamId, "chat"))).ConfigureAwait(false);
        _dm(callerSteamId, result.IsSuccess ? "Unmuted." : $"Unmute failed: {result.Error!.Message}");
    }
}
