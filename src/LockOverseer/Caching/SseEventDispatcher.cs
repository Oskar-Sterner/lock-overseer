using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Caching;

public sealed class SseEventDispatcher
{
    private readonly AuthorityCache _cache;
    private readonly IPlayerKicker _kicker;
    private readonly ILockOverseerService _svc;
    private readonly ReconcileService _reconcile;
    private readonly ILogger<SseEventDispatcher> _log;

    public SseEventDispatcher(
        AuthorityCache cache, IPlayerKicker kicker,
        ILockOverseerService svc, ReconcileService reconcile,
        ILogger<SseEventDispatcher> log)
    {
        _cache = cache; _kicker = kicker; _svc = svc;
        _reconcile = reconcile; _log = log;
    }

    public long LastEventId { get; private set; }

    public async Task DispatchAsync(SseFrame frame, CancellationToken ct)
    {
        if (frame.Id is long id && id > LastEventId) LastEventId = id;
        try
        {
            switch (frame.Event)
            {
                case "ban.created":    HandleBanCreated(frame.Data); break;
                case "ban.revoked":    HandleBanRevoked(frame.Data); break;
                case "mute.created":   HandleMuteCreated(frame.Data); break;
                case "mute.revoked":   HandleMuteRevoked(frame.Data); break;
                case "role.created":
                case "role.updated":
                case "role.deleted":   await _svc.RefreshRolesAsync(ct).ConfigureAwait(false); break;
                case "role_assignment.created":
                case "role_assignment.revoked":
                case "flag_assignment.created":
                case "flag_assignment.revoked":
                    await HandleAssignmentChange(frame.Data, ct).ConfigureAwait(false);
                    break;
                case "sync_required":  await HandleSyncRequired(frame.Data, ct).ConfigureAwait(false); break;
                default:
                    _log.LogDebug("[LockOverseer.Events] unknown event {Event}", frame.Event);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "[LockOverseer.Events] dispatch failed for {Event} (id={Id})",
                frame.Event, frame.Id);
        }
    }

    private void HandleBanCreated(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var ban = new Ban(
            Id: root.GetProperty("ban_id").GetInt64(),
            SteamId: root.GetProperty("steam_id").GetInt64(),
            Reason: OptString(root, "reason"),
            IssuedAt: root.GetProperty("issued_at").GetDateTimeOffset(),
            ExpiresAt: OptDate(root, "expires_at"),
            RevokedAt: null,
            IssuedBy: new Issuer(
                OptLong(root, "issued_by_steam_id"),
                OptString(root, "issued_by_label")),
            RevokedBy: null);
        _cache.UpsertActiveBan(ban);
        _kicker.KickBySteamId(ban.SteamId, ban.Reason ?? "banned");
    }

    private void HandleBanRevoked(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var steamId = doc.RootElement.GetProperty("steam_id").GetInt64();
        _cache.RemoveActiveBan(steamId);
    }

    private void HandleMuteCreated(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var mute = new Mute(
            Id: root.GetProperty("mute_id").GetInt64(),
            SteamId: root.GetProperty("steam_id").GetInt64(),
            Reason: OptString(root, "reason"),
            IssuedAt: root.GetProperty("issued_at").GetDateTimeOffset(),
            ExpiresAt: OptDate(root, "expires_at"),
            RevokedAt: null,
            IssuedBy: new Issuer(null, null),
            RevokedBy: null);
        _cache.UpsertActiveMute(mute);
    }

    private void HandleMuteRevoked(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var steamId = doc.RootElement.GetProperty("steam_id").GetInt64();
        _cache.RemoveActiveMute(steamId);
    }

    private async Task HandleAssignmentChange(string data, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(data);
        var steamId = doc.RootElement.GetProperty("steam_id").GetInt64();
        if (!_cache.IsConnected(steamId)) return;
        await _svc.HydrateConnectedAsync(steamId, ct).ConfigureAwait(false);
    }

    private async Task HandleSyncRequired(string data, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("latest_id", out var latest) &&
            latest.TryGetInt64(out var latestId) && latestId > LastEventId)
        {
            LastEventId = latestId;
        }
        await _reconcile.ReconcileOnceAsync(ct).ConfigureAwait(false);
    }

    private static string? OptString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    private static long? OptLong(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetInt64() : null;
    private static DateTimeOffset? OptDate(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetDateTimeOffset() : null;
}
