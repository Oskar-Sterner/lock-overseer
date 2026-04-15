namespace LockOverseer.Contracts.Models.Requests;

public sealed record FlagGrantRequest(long SteamId, string Flag, int? DurationMinutes, Issuer AssignedBy);
