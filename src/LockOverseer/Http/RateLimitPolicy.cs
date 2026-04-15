using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace LockOverseer.Http;

public static class RateLimitPolicy
{
    public const string WritePolicy = "overseer-writes";

    public static void Configure(RateLimiterOptions opts, int perMinute, int burstPerSecond)
    {
        opts.AddPolicy(WritePolicy, httpCtx =>
        {
            var key = httpCtx.Request.Headers["X-API-Key"].ToString();
            return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = burstPerSecond,
                TokensPerPeriod = Math.Max(1, perMinute / 60),
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    }
}
