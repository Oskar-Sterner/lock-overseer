namespace LockOverseer.Commands;

public readonly record struct ResolverCandidate(long SteamId, int Slot, string Name);

public enum ResolverResultKind { Resolved, NotFound, Ambiguous }

public readonly record struct ResolverResult(
    ResolverResultKind Kind,
    long SteamId,
    int? Slot,
    IReadOnlyList<ResolverCandidate> Matches);

public sealed class PlayerResolver
{
    private readonly Func<IReadOnlyList<ResolverCandidate>> _connected;

    public PlayerResolver(Func<IReadOnlyList<ResolverCandidate>> connected)
    {
        _connected = connected;
    }

    public ResolverResult Resolve(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new ResolverResult(ResolverResultKind.NotFound, 0, null, Array.Empty<ResolverCandidate>());

        if (token.Length == 17 && long.TryParse(token, out var steamId))
            return new ResolverResult(ResolverResultKind.Resolved, steamId, null, Array.Empty<ResolverCandidate>());

        if (token.StartsWith('#') && int.TryParse(token.AsSpan(1), out var slot))
        {
            foreach (var c in _connected())
                if (c.Slot == slot)
                    return new ResolverResult(ResolverResultKind.Resolved, c.SteamId, slot, Array.Empty<ResolverCandidate>());
            return new ResolverResult(ResolverResultKind.NotFound, 0, null, Array.Empty<ResolverCandidate>());
        }

        var players = _connected();
        var prefixMatches = players
            .Where(p => p.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (prefixMatches.Length == 1)
            return new ResolverResult(ResolverResultKind.Resolved, prefixMatches[0].SteamId, prefixMatches[0].Slot, Array.Empty<ResolverCandidate>());

        if (prefixMatches.Length > 1)
            return new ResolverResult(ResolverResultKind.Ambiguous, 0, null, prefixMatches);

        return new ResolverResult(ResolverResultKind.NotFound, 0, null, Array.Empty<ResolverCandidate>());
    }
}
