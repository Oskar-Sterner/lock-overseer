using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace LockOverseer.Bootstrap;

public static class PluginServices
{
    public static IServiceCollection AddAuthorityClient(
        IServiceCollection services,
        HttpMessageHandler? primaryHandler = null,
        Uri? baseAddressOverride = null)
    {
        var builder = services
            .AddHttpClient<IAuthorityClient, AuthorityClient>((sp, http) =>
            {
                var cfg = sp.GetRequiredService<IOptions<LockOverseerConfig>>().Value;
                http.BaseAddress = baseAddressOverride ?? new Uri(string.IsNullOrEmpty(cfg.AuthorityApi.BaseUrl) ? "http://127.0.0.1:8080" : cfg.AuthorityApi.BaseUrl);
                http.Timeout = TimeSpan.FromMilliseconds(cfg.AuthorityApi.TimeoutMs <= 0 ? 5000 : cfg.AuthorityApi.TimeoutMs);
            })
            .AddHttpMessageHandler(() => new IdempotencyKeyReuseHandler())
            .AddPolicyHandler((sp, _) =>
            {
                var cfg = sp.GetRequiredService<IOptions<LockOverseerConfig>>().Value;
                var delays = Backoff.DecorrelatedJitterBackoffV2(
                    medianFirstRetryDelay: TimeSpan.FromMilliseconds(200),
                    retryCount: Math.Max(1, cfg.AuthorityApi.RetryCount));
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                    .WaitAndRetryAsync(delays);
            });

        if (primaryHandler is not null)
            builder.ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services;
    }
}

internal sealed class IdempotencyKeyReuseHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Polly retries re-send the SAME HttpRequestMessage, so the header added
        // by AuthorityClient persists across retries. This handler exists as a
        // defensive guard: if a retry ever clones the request and drops the key,
        // we restore it from the Properties bag seeded on the first send.
        const string PropKey = "LO_Idempotency_Key";
        if (request.Headers.TryGetValues("Idempotency-Key", out var values))
        {
            var key = System.Linq.Enumerable.First(values);
            request.Options.Set(new HttpRequestOptionsKey<string>(PropKey), key);
        }
        else if (request.Options.TryGetValue(new HttpRequestOptionsKey<string>(PropKey), out var stored))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", stored);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
