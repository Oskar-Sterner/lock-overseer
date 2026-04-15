using System;
using System.Collections.Generic;
using System.IO;
using LockOverseer.Api;
using LockOverseer.Bootstrap;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Lifecycle;
using LockOverseer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Bootstrap;

public sealed class PluginServicesTests
{
    [Fact]
    public void BuildServiceProvider_resolves_full_object_graph()
    {
        var cfgDict = new Dictionary<string, string?>
        {
            ["AuthorityApi:BaseUrl"] = "http://127.0.0.1:8080",
            ["AuthorityApi:ApiKey"] = "k",
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(cfgDict).Build();

        var services = new ServiceCollection();
        PluginServices.AddLockOverseerCore(services, cfg, pluginDir: Path.GetTempPath());

        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<AuthorityCache>().ShouldNotBeNull();
        sp.GetRequiredService<ILockOverseerService>().ShouldBeOfType<LockOverseerService>();
        sp.GetRequiredService<IAuthorityClient>().ShouldNotBeNull();
        sp.GetRequiredService<ReconcileService>().ShouldNotBeNull();
        sp.GetRequiredService<PlaytimeTracker>().ShouldNotBeNull();
        sp.GetRequiredService<EnforcementHooks>().ShouldNotBeNull();
        sp.GetRequiredService<BootstrapAdmins>().ShouldNotBeNull();
    }
}
