using LockOverseer.Contracts;

namespace LockOverseer.Commands;

public sealed class CommandGate
{
    private readonly ILockOverseerService _service;
    private readonly Action<long, string> _dm;

    public CommandGate(ILockOverseerService service, Action<long, string> dm)
    {
        _service = service;
        _dm = dm;
    }

    public bool RequireFlag(long callerSteamId, string flag)
    {
        if (_service.HasFlag(callerSteamId, flag))
            return true;

        _dm(callerSteamId, $"Permission denied (requires `{flag}`)");
        return false;
    }
}
