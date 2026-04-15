using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api.Dto;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;

namespace LockOverseer.Api;

public interface IAuthorityClient
{
    ValueTask<Result<PlayerResource>> GetPlayerAsync(long steamId, CancellationToken ct = default);
    ValueTask<Result<PlayerResource>> UpsertPlayerAsync(long steamId, string? lastKnownName, CancellationToken ct = default);
    ValueTask<Result<PlayerResource>> AddPlaytimeAsync(long steamId, long seconds, CancellationToken ct = default);

    ValueTask<Result<IReadOnlyList<BanResource>>> GetActiveBansAsync(CancellationToken ct = default);
    ValueTask<Result<IReadOnlyList<MuteResource>>> GetActiveMutesAsync(CancellationToken ct = default);
    ValueTask<Result<IReadOnlyList<RoleResource>>> GetRolesAsync(CancellationToken ct = default);

    ValueTask<Result<BanResource>> IssueBanAsync(BanResource request, CancellationToken ct = default);
    ValueTask<Result<BanResource>> RevokeBanAsync(long banId, string? reason, IssuerResource revokedBy, CancellationToken ct = default);
    ValueTask<Result<MuteResource>> IssueMuteAsync(MuteResource request, CancellationToken ct = default);
    ValueTask<Result<MuteResource>> RevokeMuteAsync(long muteId, string? reason, IssuerResource revokedBy, CancellationToken ct = default);

    ValueTask<Result<RoleAssignmentResource>> GrantRoleAsync(long steamId, string roleName, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default);
    ValueTask<Result<RoleAssignmentResource>> RevokeRoleAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default);
    ValueTask<Result<FlagAssignmentResource>> GrantFlagAsync(long steamId, string flag, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default);
    ValueTask<Result<FlagAssignmentResource>> RevokeFlagAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default);

    ValueTask<Result<IReadOnlyList<RoleAssignmentResource>>> GetPlayerRolesAsync(long steamId, CancellationToken ct = default);
    ValueTask<Result<IReadOnlyList<FlagAssignmentResource>>> GetPlayerFlagsAsync(long steamId, CancellationToken ct = default);

    // Phase C helper lookups.
    ValueTask<Result<RoleAssignment>> GetActiveRoleAssignmentAsync(long steamId, CancellationToken ct = default);
    ValueTask<Result<FlagAssignment>> GetActiveFlagAssignmentAsync(long steamId, string flag, CancellationToken ct = default);
    ValueTask<Result<IReadOnlyList<AuditEntry>>> GetAuditAsync(int page, int pageSize, CancellationToken ct = default);
}
