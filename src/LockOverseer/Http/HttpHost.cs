using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LockOverseer.Http;

public sealed class HttpHostOptions
{
    public bool Enabled { get; set; } = true;
    public string BindAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 27080;
    public bool RequireTls { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
    public bool DocsEnabled { get; set; } = true;
}

public sealed class HttpHost : IHostedService, IAsyncDisposable
{
    private readonly HttpHostOptions _opts;
    private readonly Action<WebApplication> _configure;
    private WebApplication? _app;

    public HttpHost(HttpHostOptions options, Action<WebApplication> configure)
    { _opts = options; _configure = configure; }

    public int BoundPort { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_opts.Enabled) return;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://{_opts.BindAddress}:{_opts.Port}");
        builder.Services.AddRouting();
        var app = builder.Build();
        _configure(app);
        await app.StartAsync(ct).ConfigureAwait(false);
        var addrs = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;
        foreach (var a in addrs)
            if (Uri.TryCreate(a, UriKind.Absolute, out var u)) { BoundPort = u.Port; break; }
        _app = app;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_app is not null) await _app.StopAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync().ConfigureAwait(false);
    }
}
