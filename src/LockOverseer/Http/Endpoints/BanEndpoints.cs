using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public sealed record PostBanBody(long SteamId, int? DurationMinutes, string? Reason,
                                 long? IssuedBySteamId, string? IssuedByLabel);

public static class BanEndpoints
{
    public static void Map(IEndpointRouteBuilder app, ILockOverseerService svc)
    {
        var grp = app.MapGroup("/v1/bans");
        grp.MapPost("/", async (PostBanBody? body, CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var req = new BanRequest(body.SteamId, body.DurationMinutes, body.Reason,
                new Issuer(body.IssuedBySteamId, body.IssuedByLabel ?? "http"));
            var r = await svc.IssueBanAsync(req, ct);
            return r.IsSuccess
                ? Results.Created($"/v1/bans/{r.Value!.Id}", r.Value)
                : ProblemJson.ToHttpResult(r.Error!);
        });

        grp.MapDelete("/{id:long}", async (long id, string? reason, long? revokedBy, CancellationToken ct) =>
        {
            var r = await svc.RevokeBanAsync(id, new RevokeRequest(reason, new Issuer(revokedBy, "http")), ct);
            return r.IsSuccess ? Results.Ok(r.Value) : ProblemJson.ToHttpResult(r.Error!);
        });

        grp.MapGet("/", async (CancellationToken ct) =>
            Results.Ok(await svc.GetActiveBansAsync(ct)));
    }
}
