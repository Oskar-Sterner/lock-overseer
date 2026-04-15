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

    [Fact]
    public void Unique_prefix_resolves()
    {
        var connected = new List<ResolverCandidate>
        {
            new(76561198000000010, 1, "Alice"),
            new(76561198000000011, 2, "Bob"),
        };
        var resolver = new PlayerResolver(() => connected);

        var result = resolver.Resolve("al");

        result.Kind.ShouldBe(ResolverResultKind.Resolved);
        result.SteamId.ShouldBe(76561198000000010L);
    }

    [Fact]
    public void Ambiguous_prefix_returns_match_list()
    {
        var connected = new List<ResolverCandidate>
        {
            new(76561198000000010, 1, "Alice"),
            new(76561198000000012, 2, "Alan"),
        };
        var resolver = new PlayerResolver(() => connected);

        var result = resolver.Resolve("Al");

        result.Kind.ShouldBe(ResolverResultKind.Ambiguous);
        result.Matches.Select(m => m.Name).ShouldBe(new[] { "Alice", "Alan" }, ignoreOrder: true);
    }

    [Fact]
    public void Substring_fallback_resolves_single_hit()
    {
        var connected = new List<ResolverCandidate>
        {
            new(76561198000000020, 1, "xXAlice42Xx"),
            new(76561198000000021, 2, "Bob"),
        };
        var resolver = new PlayerResolver(() => connected);

        var result = resolver.Resolve("lice");

        result.Kind.ShouldBe(ResolverResultKind.Resolved);
        result.SteamId.ShouldBe(76561198000000020L);
    }

    [Fact]
    public void Substring_with_two_hits_is_ambiguous()
    {
        var connected = new List<ResolverCandidate>
        {
            new(76561198000000030, 1, "xXAliceXx"),
            new(76561198000000031, 2, "yYAliceYy"),
        };
        var resolver = new PlayerResolver(() => connected);

        var result = resolver.Resolve("lice");

        result.Kind.ShouldBe(ResolverResultKind.Ambiguous);
    }
}
