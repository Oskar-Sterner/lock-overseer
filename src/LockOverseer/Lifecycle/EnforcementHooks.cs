using LockOverseer.Contracts;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Lifecycle;

public sealed class EnforcementHooks
{
    private readonly ILockOverseerService _service;
    private readonly ILogger<EnforcementHooks> _log;

    public EnforcementHooks(ILockOverseerService service, ILogger<EnforcementHooks> log)
    {
        _service = service; _log = log;
    }

    public bool ShouldRejectConnect(long steamId, out string reason)
    {
        if (_service.IsBanned(steamId))
        {
            reason = "You are banned from this server.";
            _log.LogInformation("[LockOverseer.Authority] Rejecting connect for banned {SteamId}", steamId);
            return true;
        }
        reason = string.Empty;
        return false;
    }

    public bool ShouldSuppressChat(long steamId)
    {
        if (_service.IsMuted(steamId))
        {
            _log.LogDebug("[LockOverseer.Authority] Suppressing chat for muted {SteamId}", steamId);
            return true;
        }
        return false;
    }
}
