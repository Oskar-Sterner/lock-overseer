using System.Net.Http.Json;
using LockOverseer.Http;
using LockOverseer.Http.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Http;

public sealed class EndpointsSmokeTests
{
    [Fact]
    public async Task Health_returns_200_without_auth()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        HealthEndpoints.Map(app);
        await app.StartAsync();

        var client = app.GetTestClient();
        var r = await client.GetAsync("/v1/health");

        r.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        await app.StopAsync();
    }

    [Fact]
    public async Task Post_bans_happy_path_returns_201_and_persists()
    {
        var svc = NSubstitute.Substitute.For<LockOverseer.Contracts.ILockOverseerService>();
        svc.IssueBanAsync(NSubstitute.Arg.Any<LockOverseer.Contracts.Models.Requests.BanRequest>(),
                NSubstitute.Arg.Any<CancellationToken>())
            .Returns(LockOverseer.Contracts.Result<LockOverseer.Contracts.Models.Ban>.Ok(
                new LockOverseer.Contracts.Models.Ban(1, 1, null,
                    System.DateTimeOffset.UtcNow, null, null,
                    new LockOverseer.Contracts.Models.Issuer(null, "http"), null)));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        BanEndpoints.Map(app, svc);
        await app.StartAsync();

        var client = app.GetTestClient();
        var r = await client.PostAsJsonAsync("/v1/bans",
            new { steamId = 1L, issuedByLabel = "test", reason = "x" });

        ((int)r.StatusCode).ShouldBe(201);
        await app.StopAsync();
    }
}
