using System;

namespace LockOverseer.Api.Dto;

public sealed record PlayerResource(
    long SteamId,
    string? LastKnownName,
    DateTimeOffset? FirstConnectAt,
    DateTimeOffset? LastConnectAt,
    long TotalPlaytimeSeconds);
