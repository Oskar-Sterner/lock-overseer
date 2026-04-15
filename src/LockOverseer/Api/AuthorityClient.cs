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

    // Stubs for unimplemented methods (filled in next tasks).
    public ValueTask<Result<PlayerResource>> GetPlayerAsync(long steamId, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<PlayerResource>> UpsertPlayerAsync(long steamId, string? lastKnownName, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<PlayerResource>> AddPlaytimeAsync(long steamId, long seconds, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<IReadOnlyList<MuteResource>>> GetActiveMutesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<IReadOnlyList<RoleResource>>> GetRolesAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<BanResource>> IssueBanAsync(BanResource request, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<BanResource>> RevokeBanAsync(long banId, string? reason, IssuerResource revokedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<MuteResource>> IssueMuteAsync(MuteResource request, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<MuteResource>> RevokeMuteAsync(long muteId, string? reason, IssuerResource revokedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<RoleAssignmentResource>> GrantRoleAsync(long steamId, string roleName, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<RoleAssignmentResource>> RevokeRoleAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<FlagAssignmentResource>> GrantFlagAsync(long steamId, string flag, int? durationMinutes, IssuerResource assignedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<FlagAssignmentResource>> RevokeFlagAsync(long assignmentId, IssuerResource revokedBy, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<IReadOnlyList<RoleAssignmentResource>>> GetPlayerRolesAsync(long steamId, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<Result<IReadOnlyList<FlagAssignmentResource>>> GetPlayerFlagsAsync(long steamId, CancellationToken ct = default) => throw new NotImplementedException();
}
