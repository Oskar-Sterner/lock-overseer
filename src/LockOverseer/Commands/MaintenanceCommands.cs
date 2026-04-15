using LockOverseer.Contracts;

namespace LockOverseer.Commands;

public sealed class MaintenanceCommands
{
    private readonly ILockOverseerService _svc;
    private readonly CommandGate _gate;
    private readonly PlayerResolver _resolver;
    private readonly Action<long, string> _dm;
    private readonly Action<long, string> _kick;
    private readonly Func<Task> _triggerReconcile;

    public MaintenanceCommands(ILockOverseerService svc, CommandGate gate, PlayerResolver resolver,
                               Action<long, string> dm, Action<long, string> kick, Func<Task> triggerReconcile)
    { _svc = svc; _gate = gate; _resolver = resolver; _dm = dm; _kick = kick; _triggerReconcile = triggerReconcile; }

    [RequireFlag("overseer.admin")]
    public async Task HandleReloadAsync(long callerSteamId)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.admin")) return;
        await _triggerReconcile().ConfigureAwait(false);
        _dm(callerSteamId, "Reconcile triggered.");
    }

    [RequireFlag("overseer.kick")]
    public Task HandleKickAsync(long callerSteamId, IReadOnlyList<string> args)
    {
        if (!_gate.RequireFlag(callerSteamId, "overseer.kick")) return Task.CompletedTask;
        if (args.Count < 1) { _dm(callerSteamId, "Usage: /kick <player> [reason...]"); return Task.CompletedTask; }
        var target = _resolver.Resolve(args[0]);
        if (target.Kind != ResolverResultKind.Resolved) { _dm(callerSteamId, "Player not found"); return Task.CompletedTask; }
        if (!_gate.AssertOutranks(callerSteamId, target.SteamId)) return Task.CompletedTask;

        var reason = ReasonParser.JoinReason(args.Skip(1).ToArray()) ?? "kicked by admin";
        _kick(target.SteamId, reason);
        _dm(callerSteamId, "Kicked.");
        return Task.CompletedTask;
    }
}
