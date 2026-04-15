using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LockOverseer.Config;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockOverseer.Caching;

public sealed class AuthorityCache
{
    private readonly TimeProvider _time;
    private readonly LockOverseerConfig _cfg;
    private readonly ILogger<AuthorityCache> _log;

    private readonly ConcurrentDictionary<long, Ban> _activeBans = new();
    private readonly ConcurrentDictionary<long, Mute> _activeMutes = new();
    private volatile ImmutableDictionary<string, RoleDefinition> _roles = ImmutableDictionary<string, RoleDefinition>.Empty;
    private readonly ConcurrentDictionary<long, ConnectedPlayerState> _connected = new();

    public AuthorityCache(TimeProvider time, IOptions<LockOverseerConfig> cfg, ILogger<AuthorityCache> log)
    {
        _time = time; _cfg = cfg.Value; _log = log;
    }

    // ---- Hot-path reads (lock-free). ----
    public bool IsBanned(long steamId) => IsActive(_activeBans, steamId, b => b.ExpiresAt, b => b.RevokedAt);
    public bool IsMuted(long steamId) => IsActive(_activeMutes, steamId, m => m.ExpiresAt, m => m.RevokedAt);

    public string? GetRole(long steamId) =>
        _connected.TryGetValue(steamId, out var s) ? s.RoleName : null;

    public bool HasFlag(long steamId, string flag) =>
        _connected.TryGetValue(steamId, out var s) && s.EffectiveFlags.Contains(flag);

    private bool IsActive<T>(ConcurrentDictionary<long, T> store, long steamId,
        Func<T, DateTimeOffset?> expiresAt, Func<T, DateTimeOffset?> revokedAt)
    {
        if (!store.TryGetValue(steamId, out var row)) return false;
        if (revokedAt(row) is not null) return false;
        var exp = expiresAt(row);
        return exp is null || exp > _time.GetUtcNow();
    }

    // ---- Writers (called by ReconcileService and LockOverseerService). ----
    public void ReplaceActiveBans(IEnumerable<Ban> bans)
    {
        _activeBans.Clear();
        int n = 0;
        foreach (var b in bans)
        {
            if (n++ >= _cfg.Cache.MaxActiveBans)
            {
                _log.LogError("[LockOverseer.Authority] Active bans exceeds ceiling {Ceiling}; truncating", _cfg.Cache.MaxActiveBans);
                break;
            }
            _activeBans[b.SteamId] = b;
        }
    }

    public void ReplaceActiveMutes(IEnumerable<Mute> mutes)
    {
        _activeMutes.Clear();
        int n = 0;
        foreach (var m in mutes)
        {
            if (n++ >= _cfg.Cache.MaxActiveMutes)
            {
                _log.LogError("[LockOverseer.Authority] Active mutes exceeds ceiling {Ceiling}; truncating", _cfg.Cache.MaxActiveMutes);
                break;
            }
            _activeMutes[m.SteamId] = m;
        }
    }

    public void ReplaceRoles(IReadOnlyDictionary<string, RoleDefinition> roles) =>
        _roles = roles.ToImmutableDictionary();

    public IReadOnlyDictionary<string, RoleDefinition> Roles => _roles;

    public void UpsertActiveBan(Ban b) => _activeBans[b.SteamId] = b;
    public void RemoveActiveBan(long steamId) => _activeBans.TryRemove(steamId, out _);

    public void UpsertActiveMute(Mute m) => _activeMutes[m.SteamId] = m;
    public void RemoveActiveMute(long steamId) => _activeMutes.TryRemove(steamId, out _);

    public void SetConnectedState(long steamId, ConnectedPlayerState state) => _connected[steamId] = state;
    public void ClearConnected(long steamId) => _connected.TryRemove(steamId, out _);

    public IReadOnlyCollection<Ban> SnapshotActiveBans() => _activeBans.Values.ToArray();
    public IReadOnlyCollection<Mute> SnapshotActiveMutes() => _activeMutes.Values.ToArray();
    public int ConnectedCount => _connected.Count;

    // Phase C helper accessors.
    public string? GetConnectedRole(long steamId) =>
        _connected.TryGetValue(steamId, out var s) ? s.RoleName : null;

    public int? GetRolePriority(string roleName) =>
        _roles.TryGetValue(roleName, out var def) ? def.Priority : (int?)null;

    public bool TryGetActiveBan(long steamId, out Ban ban)
    {
        if (_activeBans.TryGetValue(steamId, out var row) && row.RevokedAt is null &&
            (row.ExpiresAt is null || row.ExpiresAt > _time.GetUtcNow()))
        {
            ban = row;
            return true;
        }
        ban = default!;
        return false;
    }

    public bool TryGetActiveMute(long steamId, out Mute mute)
    {
        if (_activeMutes.TryGetValue(steamId, out var row) && row.RevokedAt is null &&
            (row.ExpiresAt is null || row.ExpiresAt > _time.GetUtcNow()))
        {
            mute = row;
            return true;
        }
        mute = default!;
        return false;
    }

    // Called every ExpirySweepSeconds by ReconcileService.
    public int SweepExpired()
    {
        var now = _time.GetUtcNow();
        int removed = 0;
        foreach (var (k, b) in _activeBans)
            if (b.RevokedAt is not null || (b.ExpiresAt is not null && b.ExpiresAt <= now))
                if (_activeBans.TryRemove(k, out _)) removed++;
        foreach (var (k, m) in _activeMutes)
            if (m.RevokedAt is not null || (m.ExpiresAt is not null && m.ExpiresAt <= now))
                if (_activeMutes.TryRemove(k, out _)) removed++;
        return removed;
    }
}
