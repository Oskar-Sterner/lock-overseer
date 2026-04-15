using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public static class HealthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
    }
}
