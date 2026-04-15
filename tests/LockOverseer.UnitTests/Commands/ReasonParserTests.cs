using LockOverseer.Commands;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class ReasonParserTests
{
    [Fact]
    public void Trailing_tokens_join_as_reason()
    {
        ReasonParser.JoinReason(new[] { "being", "a", "jerk" }).ShouldBe("being a jerk");
    }

    [Fact]
    public void Quoted_segment_is_preserved_as_single_reason()
    {
        ReasonParser.JoinReason(new[] { "\"racial", "slurs\"" }).ShouldBe("racial slurs");
    }

    [Fact]
    public void Empty_returns_null()
    {
        ReasonParser.JoinReason(Array.Empty<string>()).ShouldBeNull();
    }
}
