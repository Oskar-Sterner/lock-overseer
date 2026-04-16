using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Caching;
using LockOverseer.Config;
using LockOverseer.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class AuthorityEventStreamTests
{
    private sealed class StreamHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _onSend;
        public StreamHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> onSend) => _onSend = onSend;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => _onSend(request);
    }

    private static HttpResponseMessage SseOk(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
    };

    private static (SseEventDispatcher dispatcher, AuthorityCache cache, IPlayerKicker kicker)
        BuildDispatcher()
    {
        var cache = new AuthorityCache(TimeProvider.System,
            Options.Create(new LockOverseerConfig()),
            NullLogger<AuthorityCache>.Instance);
        var kicker = Substitute.For<IPlayerKicker>();
        var svc = Substitute.For<ILockOverseerService>();
        var client = Substitute.For<IAuthorityClient>();
        var rc = new ReconcileService(client, cache,
            Options.Create(new LockOverseerConfig()),
            NullLogger<ReconcileService>.Instance, TimeProvider.System);
        var dispatcher = new SseEventDispatcher(cache, kicker, svc, rc,
            NullLogger<SseEventDispatcher>.Instance);
        return (dispatcher, cache, kicker);
    }

    [Fact]
    public async Task Dispatches_frames_and_records_last_event_id()
    {
        var (dispatcher, cache, _) = BuildDispatcher();
        int calls = 0;
        var handler = new StreamHandler(req =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? SseOk(
                    "id: 1\nevent: ban.created\ndata: {\"ban_id\":1,\"steam_id\":42,\"reason\":\"x\"," +
                    "\"issued_at\":\"2026-04-16T10:00:00+00:00\",\"expires_at\":null," +
                    "\"issued_by_steam_id\":null,\"issued_by_label\":\"sys\"}\n\n" +
                    "id: 2\nevent: ban.revoked\ndata: {\"ban_id\":1,\"steam_id\":42," +
                    "\"revoked_at\":\"2026-04-16T10:01:00+00:00\",\"revoke_reason\":null}\n\n")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://stream") };

        var cfg = Options.Create(new LockOverseerConfig
        {
            AuthorityApi = new AuthorityApiSection
            {
                Events = new EventsSection
                {
                    Enabled = true,
                    StreamPath = "/events/stream",
                    ReconnectInitialDelayMs = 10,
                    ReconnectMaxDelayMs = 50,
                    HeartbeatTimeoutMs = 45_000,
                }
            }
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("authority-events").Returns(http);

        var stream = new AuthorityEventStream(factory, dispatcher, cfg,
            NullLogger<AuthorityEventStream>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await stream.StartAsync(cts.Token);

        // Wait until both frames are dispatched or timeout.
        for (int i = 0; i < 100 && dispatcher.LastEventId < 2; i++)
            await Task.Delay(20);
        await stream.StopAsync(CancellationToken.None);

        dispatcher.LastEventId.ShouldBe(2L);
        cache.IsBanned(42).ShouldBeFalse();  // revoked event applied
    }

    [Fact]
    public async Task Sends_Last_Event_Id_header_on_reconnect()
    {
        var (dispatcher, _, _) = BuildDispatcher();
        string? secondReqHeader = null;
        int calls = 0;
        var reconnectedEvent = new ManualResetEventSlim(false);
        var handler = new StreamHandler(req =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(SseOk(
                    "id: 5\nevent: ban.revoked\ndata: {\"ban_id\":1,\"steam_id\":7," +
                    "\"revoked_at\":\"2026-04-16T10:00:00+00:00\",\"revoke_reason\":null}\n\n"));
            }
            secondReqHeader = req.Headers.TryGetValues("Last-Event-ID", out var v)
                ? string.Join(",", v)
                : null;
            reconnectedEvent.Set();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://stream") };

        var cfg = Options.Create(new LockOverseerConfig
        {
            AuthorityApi = new AuthorityApiSection
            {
                Events = new EventsSection
                {
                    Enabled = true,
                    StreamPath = "/events/stream",
                    ReconnectInitialDelayMs = 10,
                    ReconnectMaxDelayMs = 50,
                    HeartbeatTimeoutMs = 45_000,
                }
            }
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("authority-events").Returns(http);

        var stream = new AuthorityEventStream(factory, dispatcher, cfg,
            NullLogger<AuthorityEventStream>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await stream.StartAsync(cts.Token);

        // Wait for the reconnect (second request) to fire.
        reconnectedEvent.Wait(TimeSpan.FromSeconds(2));
        await stream.StopAsync(CancellationToken.None);

        secondReqHeader.ShouldBe("5");
    }
}
