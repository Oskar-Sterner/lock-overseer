using System;
using System.Collections.Concurrent;
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
    public bool IsMuted(long steamId)  => _cache.IsMuted(steamId);
    public bool HasFlag(long steamId, string flag) => _cache.HasFlag(steamId, flag);
    public string? GetRole(long steamId) => _cache.GetRole(steamId);

    public async ValueTask<PlayerRecord?> GetPlayerAsync(long steamId, CancellationToken ct = default)
    {
        var r = await _client.GetPlayerAsync(steamId, ct).ConfigureAwait(false);
        if (!r.IsSuccess) return null;
        var p = r.Value!;
        return new PlayerRecord(p.SteamId, p.LastKnownName, p.FirstConnectAt, p.LastConnectAt,
            p.TotalPlaytimeSeconds, p.CurrentRole, p.Flags,
            p.ActiveBan is null ? null : ToModel(p.ActiveBan),
            p.ActiveMute is null ? null : ToModel(p.ActiveMute));
    }

    public ValueTask<IReadOnlyList<Ban>>  GetActiveBansAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<Ban>>(_cache.SnapshotActiveBans().ToArray());
    public ValueTask<IReadOnlyList<Mute>> GetActiveMutesAsync(CancellationToken ct = default)
        => ValueTask.FromResult<IReadOnlyList<Mute>>(_cache.SnapshotActiveMutes().ToArray());

    public async ValueTask<Result<Ban>> IssueBanAsync(BanRequest r, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(r.SteamId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dto = new BanResource(0, r.SteamId, r.Reason,
                IssuedAt: DateTimeOffset.UnixEpoch,
                ExpiresAt: r.DurationMinutes is int m ? DateTimeOffset.UtcNow.AddMinutes(m) : null,
                RevokedAt: null,
                IssuedBy: new IssuerResource(r.IssuedBy.SteamId, r.IssuedBy.Label),
                RevokedBy: null);
            var api = await _client.IssueBanAsync(dto, ct).ConfigureAwait(false);
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
            var dto = new MuteResource(0, r.SteamId, r.Reason,
                IssuedAt: DateTimeOffset.UnixEpoch,
                ExpiresAt: r.DurationMinutes is int m ? DateTimeOffset.UtcNow.AddMinutes(m) : null,
                RevokedAt: null,
                IssuedBy: new IssuerResource(r.IssuedBy.SteamId, r.IssuedBy.Label),
                RevokedBy: null);
            var api = await _client.IssueMuteAsync(dto, ct).ConfigureAwait(false);
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

    // --- mappers ---
    private static Ban  ToModel(BanResource r)  => new(r.Id, r.SteamId, r.Reason, r.IssuedAt, r.ExpiresAt, r.RevokedAt, new Issuer(r.IssuedBy.SteamId, r.IssuedBy.Label), r.RevokedBy is null ? null : new Issuer(r.RevokedBy.SteamId, r.RevokedBy.Label));
    private static Mute ToModel(MuteResource r) => new(r.Id, r.SteamId, r.Reason, r.IssuedAt, r.ExpiresAt, r.RevokedAt, new Issuer(r.IssuedBy.SteamId, r.IssuedBy.Label), r.RevokedBy is null ? null : new Issuer(r.RevokedBy.SteamId, r.RevokedBy.Label));
    private static RoleAssignment ToModel(RoleAssignmentResource r) => new(r.Id, r.SteamId, r.RoleName, r.AssignedAt, r.ExpiresAt, r.RevokedAt, new Issuer(r.AssignedBy.SteamId, r.AssignedBy.Label));
    private static FlagAssignment ToModel(FlagAssignmentResource r) => new(r.Id, r.SteamId, r.Flag, r.AssignedAt, r.ExpiresAt, r.RevokedAt, new Issuer(r.AssignedBy.SteamId, r.AssignedBy.Label));
}
