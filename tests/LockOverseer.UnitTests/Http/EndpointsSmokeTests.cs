using LockOverseer.Http;
using LockOverseer.Http.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}
