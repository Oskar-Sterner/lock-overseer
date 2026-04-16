using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Config;

public sealed class BootstrapAdmins
{
    private readonly IAuthorityClient _client;
    private readonly ILogger<BootstrapAdmins> _log;

    private sealed record Entry(long SteamId, string? Label);

    public BootstrapAdmins(IAuthorityClient client, ILogger<BootstrapAdmins> log)
    {
        _client = client; _log = log;
    }

    public async Task SeedAsync(string adminsJsonPath, CancellationToken ct)
    {
        // Ensure bootstrap role definitions (admin/mod/player) exist before we
        // try to assign admin to anybody from admins.json. The external API
        // will 404 a role grant referencing a missing role, so this step must
        // succeed first. Idempotent: roles are created only if absent.
        await SeedRolesAsync(ct).ConfigureAwait(false);

        if (!File.Exists(adminsJsonPath))
        {
            _log.LogInformation("[LockOverseer.Authority] admins.json not present at {Path}; skipping bootstrap", adminsJsonPath);
            return;
        }

        var text = await File.ReadAllTextAsync(adminsJsonPath, ct).ConfigureAwait(false);
        var entries = JsonSerializer.Deserialize<List<Entry>>(text, JsonDefaults.Options) ?? new();
        if (entries.Count == 0) return;

        foreach (var e in entries)
        {
            var existing = await _client.GetPlayerRolesAsync(e.SteamId, ct).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                bool hasActiveAdmin = false;
                foreach (var a in existing.Value!)
                {
                    if (a.RoleName == "admin" && a.RevokedAt is null &&
                        (a.ExpiresAt is null || a.ExpiresAt > DateTimeOffset.UtcNow))
                    { hasActiveAdmin = true; break; }
                }
                if (hasActiveAdmin)
                {
                    _log.LogInformation("[LockOverseer.Authority] {SteamId} already has active admin role; skipping", e.SteamId);
                    continue;
                }
            }
            var r = await _client.GrantRoleAsync(e.SteamId, "admin", null,
                new IssuerResource(null, "bootstrap"), ct).ConfigureAwait(false);
            if (!r.IsSuccess)
                _log.LogWarning("[LockOverseer.Authority] Failed to seed admin {SteamId}: {Err}", e.SteamId, r.Error!.Message);
            else
                _log.LogInformation("[LockOverseer.Authority] Seeded admin role for {SteamId} ({Label})", e.SteamId, e.Label ?? "-");
        }
    }

    private static readonly (string Name, string Description, int Priority, string[] Flags)[] _defaultRoles =
    {
        ("admin", "Full moderation and administration rights.", 100, new[]
        {
            "overseer.ban", "overseer.mute", "overseer.kick",
            "overseer.role", "overseer.flag", "overseer.info", "overseer.admin",
        }),
        ("mod", "Moderation rights (ban/mute/kick/info).", 50, new[]
        {
            "overseer.ban", "overseer.mute", "overseer.kick", "overseer.info",
        }),
        ("player", "Default role, no special flags.", 0, System.Array.Empty<string>()),
    };

    public async Task SeedRolesAsync(CancellationToken ct)
    {
        var existing = await _client.GetRolesAsync(ct).ConfigureAwait(false);
        if (!existing.IsSuccess)
        {
            _log.LogWarning("[LockOverseer.Authority] Cannot fetch roles to seed bootstrap roles: {Err}", existing.Error!.Message);
            return;
        }

        var have = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in existing.Value!) have.Add(role.Name);

        foreach (var def in _defaultRoles)
        {
            if (have.Contains(def.Name))
            {
                _log.LogDebug("[LockOverseer.Authority] Bootstrap role {Role} already exists", def.Name);
                continue;
            }
            var created = await _client.CreateRoleAsync(def.Name, def.Description, def.Priority, def.Flags, ct).ConfigureAwait(false);
            if (created.IsSuccess)
                _log.LogInformation("[LockOverseer.Authority] Created bootstrap role {Role} (priority={Priority}, {FlagCount} flags)", def.Name, def.Priority, def.Flags.Length);
            else
                _log.LogWarning("[LockOverseer.Authority] Failed to create bootstrap role {Role}: {Err}", def.Name, created.Error!.Message);
        }
    }
}
