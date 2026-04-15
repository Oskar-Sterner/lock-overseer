namespace LockOverseer.Contracts;

public readonly record struct Result<T>(T? Value, AuthorityError? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(AuthorityError error) => new(default, error);
}
