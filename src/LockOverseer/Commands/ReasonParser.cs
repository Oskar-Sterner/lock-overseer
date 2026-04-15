namespace LockOverseer.Commands;

public static class ReasonParser
{
    public static string? JoinReason(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            return null;

        var joined = string.Join(' ', tokens).Trim();
        if (joined.Length == 0)
            return null;

        if (joined.StartsWith('"') && joined.EndsWith('"') && joined.Length >= 2)
            joined = joined[1..^1];

        return joined.Length == 0 ? null : joined;
    }
}
