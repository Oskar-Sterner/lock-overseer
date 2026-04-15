using LockOverseer.Bootstrap;
using LockOverseer.Caching;
using LockOverseer.Contracts;
using LockOverseer.Http;
using LockOverseer.Http.Endpoints;
using LockOverseer.Lifecycle;
using LockOverseer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LockOverseer.IntegrationTests.Fixtures;

/// <summary>
/// In-process host that composes the LockOverseer DI graph (Phase B) pointed at a
/// Testcontainers MockAPI and starts an HttpHost exposing the plugin's REST surface.
/// </summary>
public sealed class OverseerHostFixture : IAsyncLifetime
{
    private IHost? _host;
    private HttpHost? _httpHost;
    private Uri? _authority;

    public ILockOverseerService Service { get; private set; } = null!;
    public Action<long, string> OnKick { get; set; } = (_, _) => { };

    public void UseAuthority(Uri uri) => _authority = uri;

    public async Task InitializeAsync()
    {
        if (_authority is null)
            return; // Caller must invoke UseAuthority before tests run.

        var tempDir = Path.Combine(Path.GetTempPath(), $"lockoverseer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["AuthorityApi:BaseUrl"] = _authority.ToString(),
            ["AuthorityApi:ApiKey"] = "integration-test",
            ["AuthorityApi:TimeoutMs"] = "5000",
            ["AuthorityApi:RetryCount"] = "1",
            ["Cache:ReconcileIntervalSeconds"] = "300",
            ["Http:Enabled"] = "false", // we start HttpHost manually
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices((_, services) =>
        {
            PluginServices.AddLockOverseerCore(services, cfg, tempDir);
        });
        _host = builder.Build();
        await _host.StartAsync();

        Service = _host.Services.GetRequiredService<ILockOverseerService>();

        // Start an HTTP host exposing plugin endpoints (bans/mutes only for now)
        _httpHost = new HttpHost(new HttpHostOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            Port = 0,
            DocsEnabled = false,
        }, configure: app =>
        {
            HealthEndpoints.Map(app);
            BanEndpoints.Map(app, Service);
            MuteEndpoints.Map(app, Service);
            RoleEndpoints.Map(app, Service);
            FlagEndpoints.Map(app, Service);
            PlayerEndpoints.Map(app, Service);
            AuditEndpoints.Map(app, Service);
        });
        await ((IHostedService)_httpHost).StartAsync(CancellationToken.None);
    }

    public async Task ForceReconcileAsync()
    {
        var reconcile = _host!.Services.GetRequiredService<ReconcileService>();
        await reconcile.ReconcileOnceAsync(CancellationToken.None);
    }

    public HttpClient CreatePluginHttpClient()
    {
        if (_httpHost is null) throw new InvalidOperationException("HttpHost not started");
        return new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_httpHost.BoundPort}") };
    }

    public async Task DisposeAsync()
    {
        if (_httpHost is not null)
        {
            await ((IHostedService)_httpHost).StopAsync(CancellationToken.None);
            await _httpHost.DisposeAsync();
        }
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
