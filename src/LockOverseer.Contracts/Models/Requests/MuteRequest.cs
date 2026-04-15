namespace LockOverseer.Contracts.Models.Requests;

public sealed record MuteRequest(long SteamId, int? DurationMinutes, string? Reason, Issuer IssuedBy);
