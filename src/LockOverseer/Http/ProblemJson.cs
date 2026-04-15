using LockOverseer.Contracts;
using Microsoft.AspNetCore.Http;

namespace LockOverseer.Http;

public sealed record ProblemResult(int Status, string Title, string Detail, IDictionary<string, string> Headers);

public static class ProblemJson
{
    public static int StatusFor(AuthorityErrorKind kind) => kind switch
    {
        AuthorityErrorKind.Unreachable => 502,
        AuthorityErrorKind.Validation => 400,
        AuthorityErrorKind.NotFound => 404,
        AuthorityErrorKind.Conflict => 409,
        AuthorityErrorKind.Unauthorized => 401,
        _ => 500
    };

    public static ProblemResult From(AuthorityError err)
    {
        var headers = new Dictionary<string, string>();
        if (err.Kind == AuthorityErrorKind.Unreachable)
            headers["Retry-After"] = "5";
        return new ProblemResult(StatusFor(err.Kind), err.Kind.ToString(), err.Message ?? "", headers);
    }

    public static IResult ToHttpResult(AuthorityError err)
    {
        var r = From(err);
        return Results.Problem(title: r.Title, detail: r.Detail, statusCode: r.Status);
    }
}
