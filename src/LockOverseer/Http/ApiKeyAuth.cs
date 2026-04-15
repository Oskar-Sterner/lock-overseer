using Isopoh.Cryptography.Argon2;

namespace LockOverseer.Http;

public sealed record ApiKeyEntry(string Label, string Hash, bool Revoked);

public sealed class ApiKeyAuth
{
    private readonly IReadOnlyList<ApiKeyEntry> _entries;

    public ApiKeyAuth(IReadOnlyList<ApiKeyEntry> entries) => _entries = entries;

    public bool TryAuthenticate(string? rawKey, out string label)
    {
        label = "";
        if (string.IsNullOrEmpty(rawKey)) return false;

        foreach (var e in _entries)
        {
            if (e.Revoked) continue;
            if (Argon2.Verify(e.Hash, rawKey))
            {
                label = e.Label;
                return true;
            }
        }
        return false;
    }

    public static ApiKeyAuth LoadFromFile(string path)
    {
        if (!File.Exists(path)) return new ApiKeyAuth(Array.Empty<ApiKeyEntry>());
        using var stream = File.OpenRead(path);
        var entries = System.Text.Json.JsonSerializer.Deserialize<ApiKeyEntry[]>(stream)
                      ?? Array.Empty<ApiKeyEntry>();
        return new ApiKeyAuth(entries);
    }
}
