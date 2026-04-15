namespace LockOverseer.Contracts.Models.Requests;

public sealed record BanRequest(long SteamId, int? DurationMinutes, string? Reason, Issuer IssuedBy);
