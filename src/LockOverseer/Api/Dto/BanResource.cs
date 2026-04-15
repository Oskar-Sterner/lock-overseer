using System;

namespace LockOverseer.Api.Dto;

public sealed record BanResource(
    long Id, long SteamId, string? Reason,
    DateTimeOffset IssuedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt,
    IssuerResource IssuedBy, IssuerResource? RevokedBy);
