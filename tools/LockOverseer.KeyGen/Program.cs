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
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "add" && args[1] == "--label")
        {
            var label = args[2];
            var path = Environment.GetEnvironmentVariable("LOCKOVERSEER_KEYS_FILE") ?? "plugin_api_keys.json";
            var raw = KeyFileOps.AddKey(path, label);
            Console.WriteLine("=== Raw API key (copy now — never shown again) ===");
            Console.WriteLine(raw);
            Console.WriteLine($"=== Label: {label}  file: {path} ===");
            return 0;
        }
        Console.Error.WriteLine("Usage: LockOverseer.KeyGen add --label <name>");
        return 2;
    }
}
