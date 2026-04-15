namespace LockOverseer.Api.Dto;

public sealed record ProblemDetails(
    string? Type, string? Title, int? Status, string? Detail, string? Instance);
