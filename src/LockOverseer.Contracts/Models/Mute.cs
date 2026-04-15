using System;

namespace LockOverseer.Contracts.Models;

public sealed record Mute(
    long Id,
    long SteamId,
    string? Reason,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Issuer IssuedBy,
    Issuer? RevokedBy);
