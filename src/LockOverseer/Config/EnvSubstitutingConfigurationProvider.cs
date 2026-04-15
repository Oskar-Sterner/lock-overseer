using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace LockOverseer.Config;

public sealed class EnvSubstitutingConfigurationSource : IConfigurationSource
{
    private readonly IConfiguration _inner;
    public EnvSubstitutingConfigurationSource(IConfiguration inner) => _inner = inner;
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new EnvSubstitutingConfigurationProvider(_inner);
}

public sealed class EnvSubstitutingConfigurationProvider : ConfigurationProvider
{
    private static readonly Regex Token = new(@"\$\{([A-Z0-9_]+)\}", RegexOptions.Compiled);
    private readonly IConfiguration _inner;

    public EnvSubstitutingConfigurationProvider(IConfiguration inner) => _inner = inner;

    public override void Load()
    {
        Data.Clear();
        foreach (var kvp in Flatten(_inner))
        {
            Data[kvp.Key] = kvp.Value is null ? null : Substitute(kvp.Value);
        }
    }

    private static string Substitute(string input)
    {
        return Token.Replace(input, m =>
        {
            var name = m.Groups[1].Value;
            return Environment.GetEnvironmentVariable(name) ?? string.Empty;
        });
    }

    private static IEnumerable<KeyValuePair<string, string?>> Flatten(IConfiguration root)
    {
        foreach (var child in root.GetChildren())
        {
            if (!child.GetChildren().Any())
                yield return new(child.Path, child.Value);
            else
                foreach (var nested in Flatten(child))
                    yield return nested;
        }
    }
}
