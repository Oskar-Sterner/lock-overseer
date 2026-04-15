using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Config;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockOverseer.Caching;

public sealed class ReconcileService : BackgroundService
{
    private readonly IAuthorityClient _client;
    private readonly AuthorityCache _cache;
    private readonly LockOverseerConfig _cfg;
    private readonly ILogger<ReconcileService> _log;
    private readonly TimeProvider _time;
    private bool _degraded;

    public ReconcileService(
        IAuthorityClient client, AuthorityCache cache,
        IOptions<LockOverseerConfig> cfg, ILogger<ReconcileService> log,
        TimeProvider time)
    {
        _client = client; _cache = cache; _cfg = cfg.Value; _log = log; _time = time;
    }

    public DateTimeOffset? LastReconcileAt { get; private set; }
    public bool IsDegraded => _degraded;

    public async Task ReconcileOnceAsync(CancellationToken ct)
    {
        var bans = await _client.GetActiveBansAsync(ct).ConfigureAwait(false);
        var mutes = await _client.GetActiveMutesAsync(ct).ConfigureAwait(false);
        var roles = await _client.GetRolesAsync(ct).ConfigureAwait(false);

        if (!bans.IsSuccess || !mutes.IsSuccess || !roles.IsSuccess)
        {
            if (!_degraded)
            {
                _degraded = true;
                _log.LogWarning("[LockOverseer.Authority] Authority API unreachable; entering degraded mode");
            }
            return;
        }

        if (_degraded)
        {
            _degraded = false;
            _log.LogInformation("[LockOverseer.Authority] Authority API recovered");
        }

        _cache.ReplaceActiveBans(bans.Value!.Select(ToModel));
        _cache.ReplaceActiveMutes(mutes.Value!.Select(ToModel));
        _cache.ReplaceRoles(roles.Value!.ToDictionary(r => r.Name,
            r => new RoleDefinition(r.Name, r.Priority, r.Flags)));
        LastReconcileAt = _time.GetUtcNow();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ReconcileOnceAsync(ct).ConfigureAwait(false);
        int attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var delay = _degraded
                    ? TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempt, 5))))
                    : TimeSpan.FromSeconds(_cfg.Cache.ReconcileIntervalSeconds);
                await Task.Delay(delay, _time, ct).ConfigureAwait(false);
                _cache.SweepExpired();
                await ReconcileOnceAsync(ct).ConfigureAwait(false);
                attempt = _degraded ? attempt + 1 : 0;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "[LockOverseer.Authority] Reconcile loop error");
                attempt++;
            }
        }
    }

    private static Ban ToModel(BanResource r) => new(
        r.Id, r.SteamId, r.Reason, r.IssuedAt, r.ExpiresAt, r.RevokedAt,
        new Issuer(r.IssuedBySteamId, r.IssuedByLabel),
        r.RevokedBySteamId is null && r.RevokedByLabel is null && r.RevokedAt is null
            ? null
            : new Issuer(r.RevokedBySteamId, r.RevokedByLabel));

    private static Mute ToModel(MuteResource r) => new(
        r.Id, r.SteamId, r.Reason, r.IssuedAt, r.ExpiresAt, r.RevokedAt,
        new Issuer(r.IssuedBySteamId, r.IssuedByLabel),
        r.RevokedBySteamId is null && r.RevokedByLabel is null && r.RevokedAt is null
            ? null
            : new Issuer(r.RevokedBySteamId, r.RevokedByLabel));
}
