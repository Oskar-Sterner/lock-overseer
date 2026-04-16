namespace LockOverseer.Config;

public sealed class LockOverseerConfig
{
    public AuthorityApiSection AuthorityApi { get; set; } = new();
    public CacheSection Cache { get; set; } = new();
    public HttpSection Http { get; set; } = new();
    public BootstrapSection Bootstrap { get; set; } = new();
}

public sealed class AuthorityApiSection
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public EventsSection Events { get; set; } = new();
}

public sealed class CacheSection
{
    public int ReconcileIntervalSeconds { get; set; } = 300;
    public int ExpirySweepSeconds { get; set; } = 30;
    public int MaxActiveBans { get; set; } = 100_000;
    public int MaxActiveMutes { get; set; } = 100_000;
}

public sealed class HttpSection
{
    public bool Enabled { get; set; } = true;
    public string BindAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 27080;
    public bool RequireTls { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
}

public sealed class BootstrapSection
{
    public string AdminsFile { get; set; } = "admins.json";
    public bool SeedOnlyIfEmpty { get; set; } = true;
}

public sealed class EventsSection
{
    public bool Enabled { get; set; } = true;
    public string StreamPath { get; set; } = "/events/stream";
    public int ReconnectInitialDelayMs { get; set; } = 500;
    public int ReconnectMaxDelayMs { get; set; } = 30_000;
    public int HeartbeatTimeoutMs { get; set; } = 45_000;
}
