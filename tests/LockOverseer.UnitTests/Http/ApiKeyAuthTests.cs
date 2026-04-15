using Isopoh.Cryptography.Argon2;
using LockOverseer.Http;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Http;

public sealed class ApiKeyAuthTests
{
    [Fact]
    public void Valid_key_matches_hash()
    {
        var raw = "rawKey123";
        var hash = Argon2.Hash(raw);
        var auth = new ApiKeyAuth(new[] { new ApiKeyEntry("bot", hash, Revoked: false) });

        auth.TryAuthenticate(raw, out var label).ShouldBeTrue();
        label.ShouldBe("bot");
    }

    [Fact]
    public void Revoked_key_rejected()
    {
        var raw = "rawKey123";
        var hash = Argon2.Hash(raw);
        var auth = new ApiKeyAuth(new[] { new ApiKeyEntry("bot", hash, Revoked: true) });

        auth.TryAuthenticate(raw, out _).ShouldBeFalse();
    }

    [Fact]
    public void Wrong_key_rejected()
    {
        var hash = Argon2.Hash("correct");
        var auth = new ApiKeyAuth(new[] { new ApiKeyEntry("bot", hash, Revoked: false) });

        auth.TryAuthenticate("wrong", out _).ShouldBeFalse();
    }
}
