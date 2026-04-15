using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api.Dto;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockOverseer.Api;

public sealed partial class AuthorityClient : IAuthorityClient
{
    private readonly HttpClient _http;
    private readonly LockOverseerConfig _cfg;
    private readonly ILogger<AuthorityClient> _log;

    public AuthorityClient(HttpClient http, IOptions<LockOverseerConfig> cfg, ILogger<AuthorityClient> log)
    {
        _http = http;
        _cfg = cfg.Value;
        _log = log;
    }

    private sealed record PagedEnvelope<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

    public async ValueTask<Result<IReadOnlyList<BanResource>>> GetActiveBansAsync(CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<BanResource>>("/bans?active=true&page_size=1000", ct).ConfigureAwait(false);
        return r.IsSuccess
            ? Result<IReadOnlyList<BanResource>>.Ok(r.Value!.Items)
            : Result<IReadOnlyList<BanResource>>.Fail(r.Error!);
    }

    private async ValueTask<Result<T>> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, path);
            ApplyAuth(req);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return await DeserializeAsync<T>(resp, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "[LockOverseer.Authority] GET {Path} unreachable", path);
            return Result<T>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, ex.Message));
        }
    }

    private void ApplyAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_cfg.AuthorityApi.ApiKey))
            req.Headers.TryAddWithoutValidation("X-API-Key", _cfg.AuthorityApi.ApiKey);
    }

    private static async ValueTask<Result<T>> DeserializeAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            var v = await resp.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, ct).ConfigureAwait(false);
            return v is null
                ? Result<T>.Fail(new AuthorityError(AuthorityErrorKind.Unknown, "empty body"))
                : Result<T>.Ok(v);
        }

        var kind = resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AuthorityErrorKind.Unauthorized,
            HttpStatusCode.NotFound => AuthorityErrorKind.NotFound,
            HttpStatusCode.Conflict => AuthorityErrorKind.Conflict,
            HttpStatusCode.UnprocessableEntity => AuthorityErrorKind.Validation,
            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => AuthorityErrorKind.Unreachable,
            _ => AuthorityErrorKind.Unknown
        };
        string? detail = null;
        try { detail = (await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)); } catch { }
        return Result<T>.Fail(new AuthorityError(kind, $"HTTP {(int)resp.StatusCode}", detail));
    }

    public async ValueTask<Result<BanResource>> IssueBanAsync(BanResource request, CancellationToken ct = default)
        => await PostAsync<BanResource, BanResource>("/bans", request, ct).ConfigureAwait(false);

    private async ValueTask<Result<TResp>> PostAsync<TReq, TResp>(string path, TReq body, CancellationToken ct, string? idempotencyKey = null)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, options: JsonDefaults.Options)
            };
            ApplyAuth(req);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? UuidV7.NewId().ToString());
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return await DeserializeAsync<TResp>(resp, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "[LockOverseer.Authority] POST {Path} unreachable", path);
            return Result<TResp>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, ex.Message));
        }
    }

    public ValueTask<Result<PlayerResource>> GetPlayerAsync(long steamId, CancellationToken ct = default)
        => GetAsync<PlayerResource>($"/players/{steamId}", ct);

    public ValueTask<Result<PlayerResource>> UpsertPlayerAsync(long steamId, string? lastKnownName, CancellationToken ct = default)
        => PostAsync<object, PlayerResource>($"/players/{steamId}", new { last_known_name = lastKnownName }, ct);

    public ValueTask<Result<PlayerResource>> AddPlaytimeAsync(long steamId, long seconds, CancellationToken ct = default)
        => PostAsync<object, PlayerResource>($"/players/{steamId}/playtime", new { seconds }, ct);

    public async ValueTask<Result<IReadOnlyList<MuteResource>>> GetActiveMutesAsync(CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<MuteResource>>("/mutes?active=true&page_size=1000", ct).ConfigureAwait(false);
        return r.IsSuccess ? Result<IReadOnlyList<MuteResource>>.Ok(r.Value!.Items) : Result<IReadOnlyList<MuteResource>>.Fail(r.Error!);
    }

    public async ValueTask<Result<IReadOnlyList<RoleResource>>> GetRolesAsync(CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<RoleResource>>("/roles?page_size=1000", ct).ConfigureAwait(false);
        return r.IsSuccess ? Result<IReadOnlyList<RoleResource>>.Ok(r.Value!.Items) : Result<IReadOnlyList<RoleResource>>.Fail(r.Error!);
    }

    public ValueTask<Result<BanResource>> RevokeBanAsync(long banId, string? reason, IssuerResource revokedBy, CancellationToken ct = default)
        => DeleteAsync<BanResource>($"/bans/{banId}", new { reason, revoked_by = revokedBy }, ct);

    public ValueTask<Result<MuteResource>> IssueMuteAsync(MuteResource request, CancellationToken ct = default)
        => PostAsync<MuteResource, MuteResource>("/mutes", request, ct);

    public ValueTask<Result<MuteResource>> RevokeMuteAsync(long muteId, string? reason, IssuerResource revokedBy, CancellationToken ct = default)
        => DeleteAsync<MuteResource>($"/mutes/{muteId}", new { reason, revoked_by = revokedBy }, ct);

    public ValueTask<Result<RoleAssignmentResource>> GrantRoleAsync(long steamId, string roleName, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default)
        => PostAsync<object, RoleAssignmentResource>($"/players/{steamId}/roles",
            new { role_name = roleName, duration_minutes = durationMinutes, assigned_by = assignedBy }, ct);

    public ValueTask<Result<RoleAssignmentResource>> RevokeRoleAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default)
        => DeleteAsync<RoleAssignmentResource>($"/role-assignments/{assignmentId}", new { revoked_by = revokedBy }, ct);

    public ValueTask<Result<FlagAssignmentResource>> GrantFlagAsync(long steamId, string flag, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default)
        => PostAsync<object, FlagAssignmentResource>($"/players/{steamId}/flags",
            new { flag, duration_minutes = durationMinutes, assigned_by = assignedBy }, ct);

    public ValueTask<Result<FlagAssignmentResource>> RevokeFlagAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default)
        => DeleteAsync<FlagAssignmentResource>($"/flag-assignments/{assignmentId}", new { revoked_by = revokedBy }, ct);

    public async ValueTask<Result<IReadOnlyList<RoleAssignmentResource>>> GetPlayerRolesAsync(long steamId, CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<RoleAssignmentResource>>($"/players/{steamId}/roles", ct).ConfigureAwait(false);
        return r.IsSuccess ? Result<IReadOnlyList<RoleAssignmentResource>>.Ok(r.Value!.Items) : Result<IReadOnlyList<RoleAssignmentResource>>.Fail(r.Error!);
    }

    public async ValueTask<Result<IReadOnlyList<FlagAssignmentResource>>> GetPlayerFlagsAsync(long steamId, CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<FlagAssignmentResource>>($"/players/{steamId}/flags", ct).ConfigureAwait(false);
        return r.IsSuccess ? Result<IReadOnlyList<FlagAssignmentResource>>.Ok(r.Value!.Items) : Result<IReadOnlyList<FlagAssignmentResource>>.Fail(r.Error!);
    }

    public async ValueTask<Result<RoleAssignment>> GetActiveRoleAssignmentAsync(long steamId, CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<RoleAssignmentResource>>($"/players/{steamId}/roles?active=true&page_size=1", ct).ConfigureAwait(false);
        if (!r.IsSuccess) return Result<RoleAssignment>.Fail(r.Error!);
        var items = r.Value!.Items;
        if (items.Count == 0) return Result<RoleAssignment>.Fail(new AuthorityError(AuthorityErrorKind.NotFound, "no active role"));
        var a = items[0];
        return Result<RoleAssignment>.Ok(new RoleAssignment(a.Id, a.SteamId, a.RoleName, a.AssignedAt, a.ExpiresAt, a.RevokedAt, new Issuer(a.AssignedBy.SteamId, a.AssignedBy.Label)));
    }

    public async ValueTask<Result<FlagAssignment>> GetActiveFlagAssignmentAsync(long steamId, string flag, CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<FlagAssignmentResource>>($"/players/{steamId}/flags?flag={System.Uri.EscapeDataString(flag)}&active=true&page_size=1", ct).ConfigureAwait(false);
        if (!r.IsSuccess) return Result<FlagAssignment>.Fail(r.Error!);
        var items = r.Value!.Items;
        if (items.Count == 0) return Result<FlagAssignment>.Fail(new AuthorityError(AuthorityErrorKind.NotFound, "no active flag"));
        var a = items[0];
        return Result<FlagAssignment>.Ok(new FlagAssignment(a.Id, a.SteamId, a.Flag, a.AssignedAt, a.ExpiresAt, a.RevokedAt, new Issuer(a.AssignedBy.SteamId, a.AssignedBy.Label)));
    }

    public async ValueTask<Result<IReadOnlyList<AuditEntry>>> GetAuditAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var r = await GetAsync<PagedEnvelope<AuditEntry>>($"/audit?page={page}&page_size={pageSize}", ct).ConfigureAwait(false);
        return r.IsSuccess
            ? Result<IReadOnlyList<AuditEntry>>.Ok(r.Value!.Items)
            : Result<IReadOnlyList<AuditEntry>>.Fail(r.Error!);
    }

    private async ValueTask<Result<TResp>> DeleteAsync<TResp>(string path, object? body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, path);
            if (body is not null) req.Content = JsonContent.Create(body, options: JsonDefaults.Options);
            ApplyAuth(req);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", UuidV7.NewId().ToString());
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return await DeserializeAsync<TResp>(resp, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "[LockOverseer.Authority] DELETE {Path} unreachable", path);
            return Result<TResp>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, ex.Message));
        }
    }
}
