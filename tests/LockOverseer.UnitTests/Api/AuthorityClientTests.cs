using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Config;
using LockOverseer.Contracts;
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

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AuthorityErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AuthorityErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, AuthorityErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Conflict, AuthorityErrorKind.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity, AuthorityErrorKind.Validation)]
    public async Task GetActiveBansAsync_maps_status_codes_to_error_kind(HttpStatusCode code, AuthorityErrorKind kind)
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(code) { Content = new StringContent("{\"detail\":\"x\"}") });
        var sut = Build(handler);
        var r = await sut.GetActiveBansAsync(CancellationToken.None);
        r.IsSuccess.ShouldBeFalse();
        r.Error!.Kind.ShouldBe(kind);
    }

    [Fact]
    public async Task GetActiveBansAsync_returns_Unreachable_on_network_exception()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("boom"));
        var sut = Build(handler);
        var r = await sut.GetActiveBansAsync(CancellationToken.None);
        r.Error!.Kind.ShouldBe(AuthorityErrorKind.Unreachable);
    }

    [Fact]
    public async Task IssueBanAsync_sends_Idempotency_Key_header_as_uuidv7()
    {
        string? seenKey = null;
        var handler = new FakeHandler((req, _) =>
        {
            seenKey = req.Headers.TryGetValues("Idempotency-Key", out var v) ? System.Linq.Enumerable.First(v) : null;
            return FakeHandler.Json(HttpStatusCode.Created, """
                {"id":9,"steam_id":1,"reason":"x","issued_at":"2026-04-15T00:00:00Z","expires_at":null,"revoked_at":null,
                 "issued_by":{"steam_id":null,"label":"chat"},"revoked_by":null}
                """);
        });

        var sut = Build(handler);
        var req = new LockOverseer.Api.Dto.BanResource(
            0, 1, "x",
            DateTimeOffset.UtcNow, null, null,
            new LockOverseer.Api.Dto.IssuerResource(null, "chat"), null);
        var r = await sut.IssueBanAsync(req, CancellationToken.None);

        r.IsSuccess.ShouldBeTrue();
        seenKey.ShouldNotBeNull();
        Guid.TryParse(seenKey, out var parsed).ShouldBeTrue();
        ((parsed.ToByteArray()[7] & 0xF0) >> 4).ShouldBe(7); // version 7
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
