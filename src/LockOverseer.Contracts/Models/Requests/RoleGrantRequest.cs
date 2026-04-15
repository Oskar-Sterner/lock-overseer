namespace LockOverseer.Contracts.Models.Requests;

public sealed record RoleGrantRequest(long SteamId, string RoleName, int? DurationMinutes, Issuer AssignedBy);
