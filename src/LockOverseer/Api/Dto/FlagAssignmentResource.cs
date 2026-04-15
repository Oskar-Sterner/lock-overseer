using System;

namespace LockOverseer.Api.Dto;

public sealed record FlagAssignmentResource(
    long Id, long SteamId, string Flag,
    DateTimeOffset AssignedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt,
    IssuerResource AssignedBy);
