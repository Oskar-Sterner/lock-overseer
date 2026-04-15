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
        }
    }
}
