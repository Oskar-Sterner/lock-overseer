using System;
using System.Collections.Generic;
using LockOverseer.Config;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Config;

public sealed class EnvSubstitutionTests
{
    [Fact]
    public void Replaces_dollar_brace_tokens_with_environment_values()
    {
        Environment.SetEnvironmentVariable("LOCK_TEST_KEY", "real-secret");
        var inner = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthorityApi:ApiKey"] = "${LOCK_TEST_KEY}",
                ["AuthorityApi:BaseUrl"] = "http://host"
            }).Build();

        var cfg = new ConfigurationBuilder()
            .Add(new EnvSubstitutingConfigurationSource(inner))
            .Build();

        cfg["AuthorityApi:ApiKey"].ShouldBe("real-secret");
        cfg["AuthorityApi:BaseUrl"].ShouldBe("http://host");
    }

    [Fact]
    public void Leaves_value_untouched_when_env_var_missing()
    {
        Environment.SetEnvironmentVariable("LOCK_TEST_MISSING", null);
        var inner = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthorityApi:ApiKey"] = "${LOCK_TEST_MISSING}"
            }).Build();

        var cfg = new ConfigurationBuilder()
            .Add(new EnvSubstitutingConfigurationSource(inner))
            .Build();

        cfg["AuthorityApi:ApiKey"].ShouldBe("");
    }
}
