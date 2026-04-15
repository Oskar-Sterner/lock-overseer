using LockOverseer.Commands;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class DurationParserTests
{
    [Fact]
    public void Perm_returns_null_minutes()
    {
        DurationParser.TryParse("perm", out var minutes).ShouldBeTrue();
        minutes.ShouldBeNull();
    }

    [Fact]
    public void Perm_is_case_insensitive()
    {
        DurationParser.TryParse("PERM", out var minutes).ShouldBeTrue();
        minutes.ShouldBeNull();
    }

    [Fact]
    public void Positive_integer_parses()
    {
        DurationParser.TryParse("60", out var minutes).ShouldBeTrue();
        minutes.ShouldBe(60);
    }

    [Fact]
    public void Zero_or_negative_is_rejected()
    {
        DurationParser.TryParse("0", out _).ShouldBeFalse();
        DurationParser.TryParse("-5", out _).ShouldBeFalse();
    }

    [Fact]
    public void Non_numeric_is_rejected()
    {
        DurationParser.TryParse("forever", out _).ShouldBeFalse();
    }
}
