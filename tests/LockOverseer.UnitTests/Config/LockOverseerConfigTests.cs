using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;
using LockOverseer.Config;
using System.Collections.Generic;

namespace LockOverseer.UnitTests.Config;

public sealed class LockOverseerConfigTests
{
    [Fact]
    public void Binds_from_IConfiguration()
    {
        var dict = new Dictionary<string, string?>
        {
            ["AuthorityApi:BaseUrl"] = "http://api",
            ["AuthorityApi:ApiKey"] = "secret",
            ["AuthorityApi:TimeoutMs"] = "5000",
            ["AuthorityApi:RetryCount"] = "3",
            ["Cache:ReconcileIntervalSeconds"] = "300",
            ["Cache:ExpirySweepSeconds"] = "30",
            ["Cache:MaxActiveBans"] = "100000",
            ["Cache:MaxActiveMutes"] = "100000",
            ["Http:Enabled"] = "true",
            ["Http:BindAddress"] = "127.0.0.1",
            ["Http:Port"] = "27080",
            ["Http:RequireTls"] = "false",
            ["Http:RateLimitPerMinute"] = "60",
            ["Bootstrap:AdminsFile"] = "admins.json",
            ["Bootstrap:SeedOnlyIfEmpty"] = "true"
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var bound = cfg.Get<LockOverseerConfig>()!;
        bound.AuthorityApi.BaseUrl.ShouldBe("http://api");
        bound.AuthorityApi.ApiKey.ShouldBe("secret");
        bound.Cache.ReconcileIntervalSeconds.ShouldBe(300);
        bound.Http.Port.ShouldBe(27080);
        bound.Bootstrap.AdminsFile.ShouldBe("admins.json");
    }
}
