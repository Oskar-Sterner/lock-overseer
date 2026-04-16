using LockOverseer.Bootstrap;
using LockOverseer.Caching;
using LockOverseer.Contracts;
using LockOverseer.Http;
using LockOverseer.Http.Endpoints;
using LockOverseer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LockOverseer.IntegrationTests.Fixtures;

/// <summary>
/// In-process host that composes the LockOverseer DI graph (Phase B) pointed at a
/// MockAPI subprocess and starts an HttpHost exposing the plugin's REST surface.
///
/// Initialization is deferred to the first <see cref="UseAuthority"/> call because
/// xunit's IClassFixture creates the fixture BEFORE the test class constructor runs
/// (so the authority URI isn't known at <see cref="InitializeAsync"/> time).
/// </summary>
public sealed class OverseerHostFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IHost? _host;
    private HttpHost? _httpHost;
    private ILockOverseerService? _service;
    private bool _started;

    public ILockOverseerService Service =>
        _service ?? throw new InvalidOperationException(
            "OverseerHostFixture not initialized — the test's ctor must call UseAuthority() first.");

    public IServiceProvider Services =>
        _host?.Services ?? throw new InvalidOperationException(
            "OverseerHostFixture not initialized — the test's ctor must call UseAuthority() first.");

    public Action<long, string> OnKick { get; set; } = (_, _) => { };

    /// <summary>
    /// Synchronously builds the DI graph + HttpHost against the given authority URI.
    /// Call this from the test class ctor BEFORE any test method runs.
    /// Idempotent — subsequent calls are no-ops if the host is already started.
    /// </summary>
    public void UseAuthority(Uri uri, string apiKey)
    {
        _initLock.Wait();
        try
        {
            if (_started) return;
            StartAsync(uri, apiKey).GetAwaiter().GetResult();
            _started = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task StartAsync(Uri authority, string apiKey)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lockoverseer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["AuthorityApi:BaseUrl"] = authority.ToString(),
            ["AuthorityApi:ApiKey"] = apiKey,
            ["AuthorityApi:TimeoutMs"] = "5000",
            ["AuthorityApi:RetryCount"] = "1",
            ["Cache:ReconcileIntervalSeconds"] = "300",
            ["Http:Enabled"] = "false",
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices((_, services) =>
        {
            PluginServices.AddLockOverseerCore(services, cfg, tempDir);
        });
        _host = builder.Build();
        await _host.StartAsync().ConfigureAwait(false);

        var kicker = _host.Services.GetRequiredService<LockOverseer.Bootstrap.DelegatingPlayerKicker>();
        kicker.Impl = (sid, reason) => OnKick(sid, reason);

        _service = _host.Services.GetRequiredService<ILockOverseerService>();

        _httpHost = new HttpHost(new HttpHostOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            Port = 0,
            DocsEnabled = false,
        }, configure: app =>
        {
            HealthEndpoints.Map(app);
            BanEndpoints.Map(app, _service);
            MuteEndpoints.Map(app, _service);
            RoleEndpoints.Map(app, _service);
            FlagEndpoints.Map(app, _service);
            PlayerEndpoints.Map(app, _service);
            AuditEndpoints.Map(app, _service);
        });
        await ((IHostedService)_httpHost).StartAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public Task InitializeAsync() => Task.CompletedTask; // Deferred to UseAuthority.

    public async Task ForceReconcileAsync()
    {
        var reconcile = _host!.Services.GetRequiredService<ReconcileService>();
        await reconcile.ReconcileOnceAsync(CancellationToken.None).ConfigureAwait(false);
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
            await ((IHostedService)_httpHost).StopAsync(CancellationToken.None).ConfigureAwait(false);
            await _httpHost.DisposeAsync().ConfigureAwait(false);
        }
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }
    }
}
