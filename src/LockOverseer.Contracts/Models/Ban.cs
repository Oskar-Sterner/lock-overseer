using System;

namespace LockOverseer.Contracts.Models;

public sealed record Ban(
    long Id,
    long SteamId,
    string? Reason,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Issuer IssuedBy,
    Issuer? RevokedBy);
