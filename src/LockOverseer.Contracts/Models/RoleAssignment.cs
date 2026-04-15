using System;

namespace LockOverseer.Contracts.Models;

public sealed record RoleAssignment(
    long Id,
    long SteamId,
    string RoleName,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Issuer AssignedBy);
