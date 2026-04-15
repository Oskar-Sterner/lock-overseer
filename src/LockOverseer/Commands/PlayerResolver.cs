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

        return new ResolverResult(ResolverResultKind.NotFound, 0, null, Array.Empty<ResolverCandidate>());
    }
}
