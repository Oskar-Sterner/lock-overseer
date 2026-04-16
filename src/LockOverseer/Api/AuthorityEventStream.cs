using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Caching;
using LockOverseer.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LockOverseer.Api;

public sealed class AuthorityEventStream : BackgroundService
{
    private readonly IHttpClientFactory _factory;
    private readonly SseEventDispatcher _dispatcher;
    private readonly LockOverseerConfig _cfg;
    private readonly ILogger<AuthorityEventStream> _log;
    private readonly SseFrameParser _parser = new();

    public AuthorityEventStream(
        IHttpClientFactory factory, SseEventDispatcher dispatcher,
        IOptions<LockOverseerConfig> cfg, ILogger<AuthorityEventStream> log)
    {
        _factory = factory; _dispatcher = dispatcher; _cfg = cfg.Value; _log = log;
    }

    public bool IsConnected { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_cfg.AuthorityApi.Events.Enabled)
        {
            _log.LogInformation("[LockOverseer.Events] stream disabled by config");
            return;
        }
        int attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct).ConfigureAwait(false);
                attempt = 0;  // reset on clean EOF (rare)
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                var delay = Math.Min(
                    _cfg.AuthorityApi.Events.ReconnectMaxDelayMs,
                    _cfg.AuthorityApi.Events.ReconnectInitialDelayMs * (int)Math.Pow(2, Math.Min(attempt, 6)));
                if (attempt == 0)
                    _log.LogWarning(ex, "[LockOverseer.Events] stream dropped; reconnecting in {Ms}ms", delay);
                else
                    _log.LogDebug(ex, "[LockOverseer.Events] reconnect attempt {N} in {Ms}ms", attempt, delay);
                attempt++;
                try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var http = _factory.CreateClient("authority-events");
        using var req = new HttpRequestMessage(HttpMethod.Get, _cfg.AuthorityApi.Events.StreamPath);
        if (!string.IsNullOrEmpty(_cfg.AuthorityApi.ApiKey))
            req.Headers.TryAddWithoutValidation("X-API-Key", _cfg.AuthorityApi.ApiKey);
        req.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        if (_dispatcher.LastEventId > 0)
            req.Headers.TryAddWithoutValidation(
                "Last-Event-ID", _dispatcher.LastEventId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        IsConnected = true;
        try
        {
            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var buffer = new char[4096];
            var frames = new List<SseFrame>(8);
            while (!ct.IsCancellationRequested)
            {
                int n;
                using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    readCts.CancelAfter(_cfg.AuthorityApi.Events.HeartbeatTimeoutMs);
                    n = await reader.ReadAsync(buffer.AsMemory(), readCts.Token)
                                    .ConfigureAwait(false);
                }
                if (n == 0) break;
                _parser.Feed(new string(buffer, 0, n), frames);
                if (frames.Count > 0)
                {
                    foreach (var f in frames)
                        await _dispatcher.DispatchAsync(f, ct).ConfigureAwait(false);
                    frames.Clear();
                }
            }
        }
        finally { IsConnected = false; }
    }
}
