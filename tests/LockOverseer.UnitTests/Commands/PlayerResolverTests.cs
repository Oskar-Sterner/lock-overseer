using LockOverseer.Commands;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Commands;

public sealed class PlayerResolverTests
{
    private static IReadOnlyList<ResolverCandidate> NoPlayers() => Array.Empty<ResolverCandidate>();

    [Fact]
    public void Steam64_literal_resolves_even_when_offline()
    {
        var resolver = new PlayerResolver(() => NoPlayers());

        var result = resolver.Resolve("76561198012345678");

        result.Kind.ShouldBe(ResolverResultKind.Resolved);
        result.SteamId.ShouldBe(76561198012345678L);
        result.Slot.ShouldBeNull();
    }

    [Fact]
    public void Non_17_digit_integer_is_not_treated_as_steam64()
    {
        var resolver = new PlayerResolver(() => NoPlayers());

        var result = resolver.Resolve("12345");

        result.Kind.ShouldBe(ResolverResultKind.NotFound);
    }
}
