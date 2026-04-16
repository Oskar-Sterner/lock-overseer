using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Config;
using LockOverseer.Contracts;
using Microsoft.Extensions.DependencyInjection;
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
                           "revoke_reason":null,
                           "issued_by_steam_id":null,"issued_by_label":"sys",
                           "revoked_by_steam_id":null,"revoked_by_label":null}],
                 "page":1,"page_size":1000,"total":1}
                """);
        });

        var sut = Build(handler);
        var r = await sut.GetActiveBansAsync(CancellationToken.None);
        r.IsSuccess.ShouldBeTrue();
        r.Value!.Count.ShouldBe(1);
        r.Value[0].Id.ShouldBe(1);
        r.Value[0].IssuedByLabel.ShouldBe("sys");
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
                 "revoke_reason":null,
                 "issued_by_steam_id":null,"issued_by_label":"chat",
                 "revoked_by_steam_id":null,"revoked_by_label":null}
                """);
        });

        var sut = Build(handler);
        var body = new
        {
            steam_id = 1L,
            duration_minutes = (int?)null,
            reason = "x",
            issued_by = new { steam_id = (long?)null, label = "chat" },
        };
        var r = await sut.IssueBanAsync(body, CancellationToken.None);

        r.IsSuccess.ShouldBeTrue();
        seenKey.ShouldNotBeNull();
        Guid.TryParse(seenKey, out var parsed).ShouldBeTrue();
        ((parsed.ToByteArray()[7] & 0xF0) >> 4).ShouldBe(7); // version 7
    }

    [Fact]
    public async Task IssueBanAsync_sends_flat_nested_body_to_authority()
    {
        // Regression guard: plugin must POST the external API's BanCreateIn shape exactly.
        string? seenBody = null;
        var handler = new FakeHandler((req, _) =>
        {
            seenBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return FakeHandler.Json(HttpStatusCode.Created, """
                {"id":9,"steam_id":42,"reason":"x","issued_at":"2026-04-15T00:00:00Z","expires_at":null,"revoked_at":null,
                 "revoke_reason":null,"issued_by_steam_id":null,"issued_by_label":"chat",
                 "revoked_by_steam_id":null,"revoked_by_label":null}
                """);
        });

        var sut = Build(handler);
        var body = new
        {
            steam_id = 42L,
            duration_minutes = (int?)60,
            reason = "x",
            issued_by = new { steam_id = (long?)null, label = "chat" },
        };
        var r = await sut.IssueBanAsync(body, CancellationToken.None);

        r.IsSuccess.ShouldBeTrue();
        seenBody.ShouldNotBeNull();
        using var doc = System.Text.Json.JsonDocument.Parse(seenBody);
        var root = doc.RootElement;
        root.GetProperty("steam_id").GetInt64().ShouldBe(42);
        root.GetProperty("duration_minutes").GetInt32().ShouldBe(60);
        var issuedBy = root.GetProperty("issued_by");
        issuedBy.GetProperty("label").GetString().ShouldBe("chat");
    }

    [Fact]
    public async Task GrantRoleAsync_posts_to_player_roles_endpoint()
    {
        string? path = null;
        var handler = new FakeHandler((req, _) =>
        {
            path = req.RequestUri!.PathAndQuery;
            return FakeHandler.Json(HttpStatusCode.Created, """
                {"id":5,"steam_id":1,"role_name":"mod",
                 "assigned_at":"2026-04-15T00:00:00Z","expires_at":null,"revoked_at":null,
                 "assigned_by_steam_id":null,"assigned_by_label":"chat"}
                """);
        });
        var sut = Build(handler);
        var r = await sut.GrantRoleAsync(1, "mod", 60,
            new LockOverseer.Api.Dto.IssuerResource(null, "chat"), CancellationToken.None);
        r.IsSuccess.ShouldBeTrue();
        path.ShouldBe("/players/1/roles");
    }

    [Fact]
    public async Task RevokeBanAsync_sends_DELETE_with_idempotency_key()
    {
        string? method = null; string? key = null;
        var handler = new FakeHandler((req, _) =>
        {
            method = req.Method.Method;
            key = req.Headers.TryGetValues("Idempotency-Key", out var v) ? System.Linq.Enumerable.First(v) : null;
            return FakeHandler.Json(HttpStatusCode.OK, """
                {"id":1,"steam_id":1,"reason":"x","issued_at":"2026-04-15T00:00:00Z","expires_at":null,
                 "revoked_at":"2026-04-15T00:01:00Z","revoke_reason":"appeal",
                 "issued_by_steam_id":null,"issued_by_label":"chat",
                 "revoked_by_steam_id":null,"revoked_by_label":"chat"}
                """);
        });
        var sut = Build(handler);
        var r = await sut.RevokeBanAsync(1, "appeal", new LockOverseer.Api.Dto.IssuerResource(null, "chat"), default);
        r.IsSuccess.ShouldBeTrue();
        method.ShouldBe("DELETE");
        key.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Retries_on_503_with_stable_idempotency_key()
    {
        int calls = 0;
        var seenKeys = new System.Collections.Generic.List<string>();
        var handler = new FakeHandler((req, _) =>
        {
            calls++;
            if (req.Headers.TryGetValues("Idempotency-Key", out var v)) seenKeys.Add(System.Linq.Enumerable.First(v));
            if (calls < 3) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return FakeHandler.Json(HttpStatusCode.Created, """
                {"id":1,"steam_id":1,"reason":"x","issued_at":"2026-04-15T00:00:00Z","expires_at":null,"revoked_at":null,
                 "revoke_reason":null,"issued_by_steam_id":null,"issued_by_label":"chat",
                 "revoked_by_steam_id":null,"revoked_by_label":null}
                """);
        });

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.Configure<LockOverseerConfig>(_ => { });
        LockOverseer.Bootstrap.PluginServices.AddAuthorityClient(services, handler, new Uri("http://authority/"));
        var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IAuthorityClient>();

        var body = new
        {
            steam_id = 1L,
            duration_minutes = (int?)null,
            reason = "x",
            issued_by = new { steam_id = (long?)null, label = "chat" },
        };
        var r = await client.IssueBanAsync(body, CancellationToken.None);

        r.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(3);
        seenKeys.Distinct().Count().ShouldBe(1, "idempotency key must be reused across retries");
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
