using Isopoh.Cryptography.Argon2;
using LockOverseer.KeyGen;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests;

public sealed class KeyGenTests
{
    [Fact]
    public void Add_appends_hashed_entry_and_returns_raw_key()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keys_{Guid.NewGuid():N}.json");
        try
        {
            var raw = KeyFileOps.AddKey(path, label: "discord-bot");

            raw.ShouldNotBeNullOrWhiteSpace();
            var json = File.ReadAllText(path);
            json.ShouldContain("\"Label\":\"discord-bot\"");
            var entries = System.Text.Json.JsonSerializer.Deserialize<LockOverseer.Http.ApiKeyEntry[]>(json)!;
            Argon2.Verify(entries[0].Hash, raw).ShouldBeTrue();
            entries[0].Revoked.ShouldBeFalse();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Revoke_marks_entry_revoked()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keys_{Guid.NewGuid():N}.json");
        try
        {
            KeyFileOps.AddKey(path, "a");
            KeyFileOps.AddKey(path, "b");

            KeyFileOps.RevokeKey(path, label: "a").ShouldBeTrue();

            var entries = System.Text.Json.JsonSerializer.Deserialize<LockOverseer.Http.ApiKeyEntry[]>(File.ReadAllText(path))!;
            entries.Single(e => e.Label == "a").Revoked.ShouldBeTrue();
            entries.Single(e => e.Label == "b").Revoked.ShouldBeFalse();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
