using System.Collections.Generic;

namespace LockOverseer.Caching;

public sealed record RoleDefinition(string Name, int Priority, IReadOnlyCollection<string> Flags);
