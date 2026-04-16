using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Caching;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Services;

public sealed class LockOverseerService : ILockOverseerService
{
    private readonly IAuthorityClient _client;
    private readonly AuthorityCache _cache;
    private readonly ILogger<LockOverseerService> _log;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    public LockOverseerService(IAuthorityClient client, AuthorityCache cache, ILogger<LockOverseerService> log)
    {
        _client = client; _cache = cache; _log = log;
    }

    public bool IsBanned(long steamId) => _cache.IsBanned(steamId);
    public bool IsMuted(long steamId) => _cache.IsMuted(steamId);
    public bool HasFlag(long steamId, string flag) => _cache.HasFlag(steamId, flag);
    public string? GetRole(long steamId) => _cache.GetRole(steamId);

    public async ValueTask<PlayerRecord?> GetPlayerAsync(long steamId, CancellationToken ct = default)
    {
        var r = await _client.GetPlayerAsync(steamId, ct).ConfigureAwait(false);
        if (!r.IsSuccess) return null;
        var p = r.Value!;
        // MockAPI's PlayerOut is minimal. Enrich from the local cache + connected state
        // for fields the authority doesn't include in the player resource.
        var role = _cache.GetConnectedRole(steamId) ?? _cache.GetRole(steamId);
        var flags = _cache.GetFlagsSnapshot(steamId);
        Ban? activeBan = _cache.TryGetActiveBan(steamId, out var b) ? b : null;
        Mute? activeMute = _cache.TryGetActiveMute(steamId, out var m) ? m : null;
        return new PlayerRecord(p.SteamId, p.LastKnownName, p.FirstConnectAt, p.LastConnectAt,
            p.TotalPlaytimeSeconds, role, flags, activeBan, activeMute);
    }

    public ValueTask<IReadOnlyList<Ban>> GetActiveBansAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<Ban>>(_cache.SnapshotActiveBans().ToArray());
    public ValueTask<IReadOnlyList<Mute>> GetActiveMutesAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<Mute>>(_cache.SnapshotActiveMutes().ToArray());

    public async ValueTask<Result<Ban>> IssueBanAsync(BanRequest r, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(r.SteamId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var body = new
            {
                steam_id = r.SteamId,
                duration_minutes = r.DurationMinutes,
                reason = r.Reason,
                issued_by = new { steam_id = r.IssuedBy.SteamId, label = r.IssuedBy.Label },
            };
            var api = await _client.IssueBanAsync(body, ct).ConfigureAwait(false);
            if (!api.IsSuccess) return Result<Ban>.Fail(api.Error!);
            var model = ToModel(api.Value!);
            _cache.UpsertActiveBan(model);
            return Result<Ban>.Ok(model);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<Result<Ban>> RevokeBanAsync(long banId, RevokeRequest r, CancellationToken ct = default)
    {
        var api = await _client.RevokeBanAsync(banId, r.Reason,
            new IssuerResource(r.RevokedBy.SteamId, r.RevokedBy.Label), ct).ConfigureAwait(false);
        if (!api.IsSuccess) return Result<Ban>.Fail(api.Error!);
        var model = ToModel(api.Value!);
        _cache.RemoveActiveBan(model.SteamId);
        return Result<Ban>.Ok(model);
    }

    public async ValueTask<Result<Mute>> IssueMuteAsync(MuteRequest r, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(r.SteamId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var body = new
            {
                steam_id = r.SteamId,
                duration_minutes = r.DurationMinutes,
                reason = r.Reason,
                issued_by = new { steam_id = r.IssuedBy.SteamId, label = r.IssuedBy.Label },
            };
            var api = await _client.IssueMuteAsync(body, ct).ConfigureAwait(false);
            if (!api.IsSuccess) return Result<Mute>.Fail(api.Error!);
            var model = ToModel(api.Value!);
            _cache.UpsertActiveMute(model);
            return Result<Mute>.Ok(model);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<Result<Mute>> RevokeMuteAsync(long muteId, RevokeRequest r, CancellationToken ct = default)
    {
        var api = await _client.RevokeMuteAsync(muteId, r.Reason,
            new IssuerResource(r.RevokedBy.SteamId, r.RevokedBy.Label), ct).ConfigureAwait(false);
        if (!api.IsSuccess) return Result<Mute>.Fail(api.Error!);
        var model = ToModel(api.Value!);
        _cache.RemoveActiveMute(model.SteamId);
        return Result<Mute>.Ok(model);
    }

    public async ValueTask<Result<RoleAssignment>> GrantRoleAsync(RoleGrantRequest r, CancellationToken ct = default)
    {
        var api = await _client.GrantRoleAsync(r.SteamId, r.RoleName, r.DurationMinutes,
            new IssuerResource(r.AssignedBy.SteamId, r.AssignedBy.Label), ct).ConfigureAwait(false);
        return api.IsSuccess
            ? Result<RoleAssignment>.Ok(ToModel(api.Value!))
            : Result<RoleAssignment>.Fail(api.Error!);
    }

    public async ValueTask<Result<RoleAssignment>> RevokeRoleAsync(long assignmentId, RevokeRequest r, CancellationToken ct = default)
    {
        var api = await _client.RevokeRoleAsync(assignmentId, new IssuerResource(r.RevokedBy.SteamId, r.RevokedBy.Label), ct).ConfigureAwait(false);
        return api.IsSuccess
            ? Result<RoleAssignment>.Ok(ToModel(api.Value!))
            : Result<RoleAssignment>.Fail(api.Error!);
    }

    public async ValueTask<Result<FlagAssignment>> GrantFlagAsync(FlagGrantRequest r, CancellationToken ct = default)
    {
        var api = await _client.GrantFlagAsync(r.SteamId, r.Flag, r.DurationMinutes,
            new IssuerResource(r.AssignedBy.SteamId, r.AssignedBy.Label), ct).ConfigureAwait(false);
        return api.IsSuccess
            ? Result<FlagAssignment>.Ok(ToModel(api.Value!))
            : Result<FlagAssignment>.Fail(api.Error!);
    }

    public async ValueTask<Result<FlagAssignment>> RevokeFlagAsync(long assignmentId, RevokeRequest r, CancellationToken ct = default)
    {
        var api = await _client.RevokeFlagAsync(assignmentId, new IssuerResource(r.RevokedBy.SteamId, r.RevokedBy.Label), ct).ConfigureAwait(false);
        return api.IsSuccess
            ? Result<FlagAssignment>.Ok(ToModel(api.Value!))
            : Result<FlagAssignment>.Fail(api.Error!);
    }

    public int GetRolePriority(long steamId)
    {
        var role = _cache.GetConnectedRole(steamId);
        if (role is null) return 0;
        return _cache.GetRolePriority(role) ?? 0;
    }

    public ValueTask<long?> GetActiveBanIdAsync(long steamId, CancellationToken ct = default)
    {
        return ValueTask.FromResult(_cache.TryGetActiveBan(steamId, out var ban) ? ban.Id : (long?)null);
    }

    public ValueTask<long?> GetActiveMuteIdAsync(long steamId, CancellationToken ct = default)
    {
        return ValueTask.FromResult(_cache.TryGetActiveMute(steamId, out var mute) ? mute.Id : (long?)null);
    }

    public async ValueTask<long?> GetActiveRoleAssignmentIdAsync(long steamId, CancellationToken ct = default)
    {
        var result = await _client.GetActiveRoleAssignmentAsync(steamId, ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value!.Id : null;
    }

    public async ValueTask<long?> GetActiveFlagAssignmentIdAsync(long steamId, string flag, CancellationToken ct = default)
    {
        var result = await _client.GetActiveFlagAssignmentAsync(steamId, flag, ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value!.Id : null;
    }

    public async ValueTask<IReadOnlyList<AuditEntry>> GetAuditAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _client.GetAuditAsync(page, pageSize, ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : Array.Empty<AuditEntry>();
    }

    /// <summary>
    /// Populates the connected-player cache slot (role + effective flag set) for a Steam64.
    /// Called from the plugin's OnClientConnect hook so HasFlag/GetRole answer correctly
    /// for chat-command permission checks.
    /// </summary>
    public async Task HydrateConnectedAsync(long steamId, CancellationToken ct = default)
    {
        string? roleName = null;
        var roleResult = await _client.GetActiveRoleAssignmentAsync(steamId, ct).ConfigureAwait(false);
        if (roleResult.IsSuccess) roleName = roleResult.Value!.RoleName;

        var effective = new HashSet<string>(StringComparer.Ordinal);
        if (roleName is not null && _cache.Roles.TryGetValue(roleName, out var def))
            foreach (var f in def.Flags) effective.Add(f);

        var flagsResult = await _client.GetPlayerFlagsAsync(steamId, ct).ConfigureAwait(false);
        if (flagsResult.IsSuccess)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var a in flagsResult.Value!)
            {
                if (a.RevokedAt is not null) continue;
                if (a.ExpiresAt is DateTimeOffset exp && exp <= now) continue;
                effective.Add(a.Flag);
            }
        }

        _cache.SetConnectedState(steamId,
            new ConnectedPlayerState(roleName, effective.ToFrozenSet(StringComparer.Ordinal)));
        _log.LogInformation("[LockOverseer.Authority] Hydrated connected state for {SteamId}: role={Role} flags={Count}",
            steamId, roleName ?? "-", effective.Count);
    }

    public async Task RefreshRolesAsync(CancellationToken ct = default)
    {
        var roles = await _client.GetRolesAsync(ct).ConfigureAwait(false);
        if (!roles.IsSuccess) return;
        _cache.ReplaceRoles(roles.Value!.ToDictionary(
            r => r.Name,
            r => new RoleDefinition(r.Name, r.Priority, r.Flags)));
    }

    // --- mappers (flat issuer fields, matching MockAPI BanOut/MuteOut/*AssignmentOut) ---
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

    private static RoleAssignment ToModel(RoleAssignmentResource r) => new(
        r.Id, r.SteamId, r.RoleName, r.AssignedAt, r.ExpiresAt, r.RevokedAt,
        new Issuer(r.AssignedBySteamId, r.AssignedByLabel));

    private static FlagAssignment ToModel(FlagAssignmentResource r) => new(
        r.Id, r.SteamId, r.Flag, r.AssignedAt, r.ExpiresAt, r.RevokedAt,
        new Issuer(r.AssignedBySteamId, r.AssignedByLabel));
}
