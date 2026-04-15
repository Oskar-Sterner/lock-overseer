using LockOverseer.Contracts;
using LockOverseer.Http;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Http;

public sealed class ProblemJsonTests
{
    [Theory]
    [InlineData(AuthorityErrorKind.Unreachable, 502)]
    [InlineData(AuthorityErrorKind.Validation, 400)]
    [InlineData(AuthorityErrorKind.NotFound, 404)]
    [InlineData(AuthorityErrorKind.Conflict, 409)]
    [InlineData(AuthorityErrorKind.Unauthorized, 401)]
    [InlineData(AuthorityErrorKind.Unknown, 500)]
    public void Maps_error_kind_to_status(AuthorityErrorKind kind, int status)
    {
        ProblemJson.StatusFor(kind).ShouldBe(status);
    }

    [Fact]
    public void Unreachable_adds_retry_after_header()
    {
        var err = new AuthorityError(AuthorityErrorKind.Unreachable, "api down");
        var result = ProblemJson.From(err);
        result.Headers.ShouldContainKeyAndValue("Retry-After", "5");
        result.Status.ShouldBe(502);
    }
}
