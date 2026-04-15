using LockOverseer.Contracts;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Contracts;

public sealed class ResultTests
{
    [Fact]
    public void Ok_wraps_value_and_reports_success()
    {
        Result<int> r = Result<int>.Ok(42);
        r.IsSuccess.ShouldBeTrue();
        r.Value.ShouldBe(42);
        r.Error.ShouldBeNull();
    }

    [Fact]
    public void Fail_stores_error_and_reports_failure()
    {
        var err = new AuthorityError(AuthorityErrorKind.Conflict, "dup", "existing");
        Result<string> r = Result<string>.Fail(err);
        r.IsSuccess.ShouldBeFalse();
        r.Value.ShouldBeNull();
        r.Error.ShouldBe(err);
    }
}
