using System;

namespace LockOverseer.Contracts.Models;

public sealed record AuditEntry(
    long Id,
    DateTimeOffset At,
    string Action,
    long? SubjectSteamId,
    long? ActorSteamId,
    string? ActorLabel,
    string? DetailJson);
