using LockOverseer.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public static class PlayerEndpoints
{
    public static void Map(IEndpointRouteBuilder app, ILockOverseerService svc)
    {
        var grp = app.MapGroup("/v1/players");
        grp.MapGet("/{steamId:long}", async (long steamId, CancellationToken ct) =>
        {
            var p = await svc.GetPlayerAsync(steamId, ct);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });
    }
}
