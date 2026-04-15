namespace LockOverseer.Contracts.Models.Requests;

public sealed record RevokeRequest(string? Reason, Issuer RevokedBy);
