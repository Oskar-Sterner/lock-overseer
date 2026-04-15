namespace LockOverseer.Contracts;

public enum AuthorityErrorKind
{
    Unreachable,
    Unauthorized,
    NotFound,
    Conflict,
    Validation,
    Unknown
}

public sealed record AuthorityError(AuthorityErrorKind Kind, string Message, string? Detail = null);
