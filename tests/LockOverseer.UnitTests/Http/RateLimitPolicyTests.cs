using LockOverseer.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Http;

public sealed class RateLimitPolicyTests
{
    [Fact]
    public void Can_register_policies_on_services()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRateLimiter(opts => RateLimitPolicy.Configure(opts, perMinute: 60, burstPerSecond: 10));
        var provider = builder.Services.BuildServiceProvider();

        Should.NotThrow(() => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimiterOptions>>());
    }
}
