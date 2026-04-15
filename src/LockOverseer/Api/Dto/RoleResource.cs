using System.Collections.Generic;

namespace LockOverseer.Api.Dto;

public sealed record RoleResource(string Name, string? Description, int Priority, IReadOnlyList<string> Flags);
