using LockOverseer.Contracts;
using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public sealed record PostFlagBody(string Flag, int? DurationMinutes,
                                   long? AssignedBySteamId, string? AssignedByLabel);

public static class FlagEndpoints
{
    public static void Map(IEndpointRouteBuilder app, ILockOverseerService svc)
    {
        app.MapPost("/v1/players/{steamId:long}/flags", async (long steamId, PostFlagBody? body, CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var req = new FlagGrantRequest(steamId, body.Flag, body.DurationMinutes,
                new Issuer(body.AssignedBySteamId, body.AssignedByLabel ?? "http"));
            var r = await svc.GrantFlagAsync(req, ct);
            return r.IsSuccess
                ? Results.Created($"/v1/flag-assignments/{r.Value!.Id}", r.Value)
                : ProblemJson.ToHttpResult(r.Error!);
        });

        app.MapDelete("/v1/flag-assignments/{id:long}", async (long id, string? reason, long? revokedBy, CancellationToken ct) =>
        {
            var r = await svc.RevokeFlagAsync(id, new RevokeRequest(reason, new Issuer(revokedBy, "http")), ct);
            return r.IsSuccess ? Results.Ok(r.Value) : ProblemJson.ToHttpResult(r.Error!);
        });
    }
}
