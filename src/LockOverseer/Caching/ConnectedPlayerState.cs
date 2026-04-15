using System.Collections.Frozen;

namespace LockOverseer.Caching;

public sealed record ConnectedPlayerState(string? RoleName, FrozenSet<string> EffectiveFlags);
