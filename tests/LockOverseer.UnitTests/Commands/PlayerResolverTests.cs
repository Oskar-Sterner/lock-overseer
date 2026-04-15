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

    [Fact]
    public void Slot_token_resolves_connected_player()
    {
        var connected = new List<ResolverCandidate>
        {
            new(76561198000000001, 0, "Alice"),
            new(76561198000000002, 3, "Bob"),
        };
        var resolver = new PlayerResolver(() => connected);

        var result = resolver.Resolve("#3");

        result.Kind.ShouldBe(ResolverResultKind.Resolved);
        result.SteamId.ShouldBe(76561198000000002L);
        result.Slot.ShouldBe(3);
    }

    [Fact]
    public void Unknown_slot_is_not_found()
    {
        var resolver = new PlayerResolver(() => Array.Empty<ResolverCandidate>());

        var result = resolver.Resolve("#7");

        result.Kind.ShouldBe(ResolverResultKind.NotFound);
    }
}
