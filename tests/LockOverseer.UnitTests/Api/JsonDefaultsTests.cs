using System.Text.Json;
using LockOverseer.Api;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class JsonDefaultsTests
{
    private sealed record Foo(long SteamId, string? LastKnownName);

    [Fact]
    public void Serializes_with_snake_case()
    {
        var json = JsonSerializer.Serialize(new Foo(1, "bob"), JsonDefaults.Options);
        json.ShouldContain("\"steam_id\":1");
        json.ShouldContain("\"last_known_name\":\"bob\"");
    }

    [Fact]
    public void Deserializes_snake_case()
    {
        var f = JsonSerializer.Deserialize<Foo>("{\"steam_id\":2,\"last_known_name\":null}", JsonDefaults.Options)!;
        f.SteamId.ShouldBe(2);
        f.LastKnownName.ShouldBeNull();
    }
}
