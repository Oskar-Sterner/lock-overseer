using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class AuthorityClientTests
{
    private static AuthorityClient Build(FakeHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://authority/") };
        var cfg = Options.Create(new LockOverseerConfig());
        return new AuthorityClient(http, cfg, NullLogger<AuthorityClient>.Instance);
    }

    [Fact]
    public async Task GetActiveBansAsync_returns_parsed_list_on_2xx()
    {
        var handler = new FakeHandler((req, _) =>
        {
            req.Method.ShouldBe(HttpMethod.Get);
            req.RequestUri!.PathAndQuery.ShouldStartWith("/bans");
            return FakeHandler.Json(HttpStatusCode.OK, """
                {"items":[{"id":1,"steam_id":1,"reason":null,
                           "issued_at":"2026-04-15T00:00:00Z","expires_at":null,"revoked_at":null,
                           "issued_by":{"steam_id":null,"label":"sys"},"revoked_by":null}],
                 "page":1,"page_size":1000,"total":1}
                """);
        });

        var sut = Build(handler);
        var r = await sut.GetActiveBansAsync(CancellationToken.None);
        r.IsSuccess.ShouldBeTrue();
        r.Value!.Count.ShouldBe(1);
        r.Value[0].Id.ShouldBe(1);
    }
}

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _h;
    public int CallCount { get; private set; }
    public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> h) => _h = h;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_h(req, ct));
    }
    public static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
