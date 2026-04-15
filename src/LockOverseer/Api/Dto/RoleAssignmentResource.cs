using System;

namespace LockOverseer.Api.Dto;

public sealed record RoleAssignmentResource(
    long Id,
    long SteamId,
    string RoleName,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    long? AssignedBySteamId,
    string? AssignedByLabel);
