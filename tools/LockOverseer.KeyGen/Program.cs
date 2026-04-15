using LockOverseer.Http;

namespace LockOverseer.KeyGen;

public static class KeyFileOps
{
    public static string AddKey(string path, string label)
    {
        var raw = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var hash = Isopoh.Cryptography.Argon2.Argon2.Hash(raw);
        var entries = File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<List<ApiKeyEntry>>(File.ReadAllText(path)) ?? new()
            : new List<ApiKeyEntry>();
        entries.Add(new ApiKeyEntry(label, hash, Revoked: false));
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(entries));
        return raw;
    }

    public static bool RevokeKey(string path, string label)
    {
        if (!File.Exists(path)) return false;
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<ApiKeyEntry>>(File.ReadAllText(path)) ?? new();
        var idx = entries.FindIndex(e => e.Label == label);
        if (idx < 0) return false;
        entries[idx] = entries[idx] with { Revoked = true };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(entries));
        return true;
    }

    public static IReadOnlyList<ApiKeyEntry> ListKeys(string path)
    {
        if (!File.Exists(path)) return Array.Empty<ApiKeyEntry>();
        return System.Text.Json.JsonSerializer.Deserialize<ApiKeyEntry[]>(File.ReadAllText(path)) ?? Array.Empty<ApiKeyEntry>();
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        var path = Environment.GetEnvironmentVariable("LOCKOVERSEER_KEYS_FILE") ?? "plugin_api_keys.json";

        if (args.Length >= 3 && args[0] == "add" && args[1] == "--label")
        {
            var label = args[2];
            var raw = KeyFileOps.AddKey(path, label);
            Console.WriteLine("=== Raw API key (copy now — never shown again) ===");
            Console.WriteLine(raw);
            Console.WriteLine($"=== Label: {label}  file: {path} ===");
            return 0;
        }

        if (args.Length >= 1 && args[0] == "list")
        {
            foreach (var e in KeyFileOps.ListKeys(path))
                Console.WriteLine($"{e.Label}\trevoked={e.Revoked}");
            return 0;
        }

        if (args.Length >= 3 && args[0] == "revoke" && args[1] == "--label")
        {
            var ok = KeyFileOps.RevokeKey(path, args[2]);
            Console.WriteLine(ok ? "revoked" : "label not found");
            return ok ? 0 : 1;
        }

        Console.Error.WriteLine("Usage: LockOverseer.KeyGen add --label <name> | list | revoke --label <name>");
        return 2;
    }
}
