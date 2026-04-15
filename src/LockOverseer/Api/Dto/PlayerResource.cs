using System;
using System.Collections.Generic;

namespace LockOverseer.Api.Dto;

public sealed record PlayerResource(
    long SteamId, string? LastKnownName,
    DateTimeOffset? FirstConnectAt, DateTimeOffset? LastConnectAt,
    long TotalPlaytimeSeconds,
    string? CurrentRole, IReadOnlyList<string> Flags,
    BanResource? ActiveBan, MuteResource? ActiveMute);
