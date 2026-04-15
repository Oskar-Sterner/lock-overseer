using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;

namespace LockOverseer.Contracts;

public interface ILockOverseerService
{
    bool IsBanned(long steamId);
    bool IsMuted(long steamId);
    bool HasFlag(long steamId, string flag);
    string? GetRole(long steamId);

    ValueTask<PlayerRecord?> GetPlayerAsync(long steamId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Ban>> GetActiveBansAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<Mute>> GetActiveMutesAsync(CancellationToken ct = default);

    ValueTask<Result<Ban>> IssueBanAsync(BanRequest r, CancellationToken ct = default);
    ValueTask<Result<Ban>> RevokeBanAsync(long banId, RevokeRequest r, CancellationToken ct = default);
    ValueTask<Result<Mute>> IssueMuteAsync(MuteRequest r, CancellationToken ct = default);
    ValueTask<Result<Mute>> RevokeMuteAsync(long muteId, RevokeRequest r, CancellationToken ct = default);
    ValueTask<Result<RoleAssignment>> GrantRoleAsync(RoleGrantRequest r, CancellationToken ct = default);
    ValueTask<Result<RoleAssignment>> RevokeRoleAsync(long assignmentId, RevokeRequest r, CancellationToken ct = default);
    ValueTask<Result<FlagAssignment>> GrantFlagAsync(FlagGrantRequest r, CancellationToken ct = default);
    ValueTask<Result<FlagAssignment>> RevokeFlagAsync(long assignmentId, RevokeRequest r, CancellationToken ct = default);
}
