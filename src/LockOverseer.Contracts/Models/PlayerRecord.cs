using System;
using System.Collections.Generic;

namespace LockOverseer.Contracts.Models;

public sealed record PlayerRecord(
    long SteamId,
    string? LastKnownName,
    DateTimeOffset? FirstConnectAt,
    DateTimeOffset? LastConnectAt,
    long TotalPlaytimeSeconds,
    string? CurrentRole,
    IReadOnlyList<string> Flags,
    Ban? ActiveBan,
    Mute? ActiveMute);
