using System;

namespace LockOverseer.Contracts.Models;

public sealed record FlagAssignment(
    long Id,
    long SteamId,
    string Flag,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Issuer AssignedBy);
