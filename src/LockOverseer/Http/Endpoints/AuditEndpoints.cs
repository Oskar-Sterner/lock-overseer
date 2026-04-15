using LockOverseer.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public static class AuditEndpoints
{
    public static void Map(IEndpointRouteBuilder app, ILockOverseerService svc)
    {
        app.MapGet("/v1/audit", async (int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await svc.GetAuditAsync(page ?? 1, pageSize ?? 50, ct)));
    }
}
