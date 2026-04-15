using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LockOverseer.Http.Endpoints;

public sealed record LiveOnlineEntry(long SteamId, int Slot, string Name);
public sealed record LiveServerInfo(string Map, int Tickrate);
public sealed record LiveSessionInfo(long SteamId, int ElapsedSeconds);
public sealed record KickBody(long SteamId, string? Reason);
public sealed record BroadcastBody(string Message);

public static class LiveStateEndpoints
{
    public static void Map(IEndpointRouteBuilder app,
        Func<IReadOnlyList<LiveOnlineEntry>> onlineProvider,
        Func<LiveServerInfo> serverInfoProvider,
        Func<long, LiveSessionInfo?> sessionProvider,
        Action<long, string> kickAction,
        Action<string> broadcastAction)
    {
        app.MapGet("/v1/online", () => Results.Ok(onlineProvider()));
        app.MapGet("/v1/server", () => Results.Ok(serverInfoProvider()));
        app.MapGet("/v1/players/{steamId:long}/session", (long steamId) =>
        {
            var s = sessionProvider(steamId);
            return s is null ? Results.NotFound() : Results.Ok(s);
        });
        app.MapPost("/v1/kick", (KickBody? b) =>
        {
            if (b is null) return Results.BadRequest();
            kickAction(b.SteamId, b.Reason ?? "kicked");
            return Results.Accepted();
        });
        app.MapPost("/v1/chat/broadcast", (BroadcastBody? b) =>
        {
            if (b is null) return Results.BadRequest();
            broadcastAction(b.Message);
            return Results.Accepted();
        });
    }
}
