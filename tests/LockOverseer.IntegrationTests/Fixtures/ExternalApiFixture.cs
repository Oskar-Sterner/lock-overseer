using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace LockOverseer.IntegrationTests.Fixtures;

/// <summary>
/// Launches a local reference implementation of the external Authority API
/// (`uv run lockoverseer-mockapi serve`) as a subprocess against a fresh SQLite
/// database and a freshly-provisioned API key.
///
/// This exercises the full plugin ↔ external API HTTP contract without requiring
/// Docker or Testcontainers. The <see cref="Available"/> property is true once
/// the subprocess is up and healthy; it is false when <c>uv</c> or the local
/// reference-impl package is missing on the host.
/// </summary>
public sealed class ExternalApiFixture : IAsyncLifetime
{
    private Process? _proc;
    private string? _workDir;
    private string? _tempDir;

    public Uri BaseUri { get; private set; } = null!;
    public string ApiKey { get; private set; } = "";
    public bool Available { get; private set; }
    public string? UnavailableReason { get; private set; }

    /// <summary>Legacy alias kept so existing tests keep compiling.</summary>
    public bool DockerAvailable => Available;

    public async Task InitializeAsync()
    {
        try
        {
            _workDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "..", "MockAPI"));

            if (!File.Exists(Path.Combine(_workDir, "pyproject.toml")))
            {
                UnavailableReason = $"external API reference impl not found at {_workDir}";
                return;
            }

            _tempDir = Path.Combine(Path.GetTempPath(), $"lockoverseer_extapi_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            var dbPath = Path.Combine(_tempDir, "external_api.db");
            var dbUrl = $"sqlite+aiosqlite:///{dbPath}";

            // 1. Provision an API key (also creates + migrates the DB).
            var key = await ProvisionKeyAsync(dbUrl).ConfigureAwait(false);
            if (key is null)
            {
                UnavailableReason = "Failed to provision API key via uv run lockoverseer-mockapi keys new";
                return;
            }
            ApiKey = key;

            // 2. Pick a free port and launch the server.
            var port = FindFreePort();
            _proc = StartServer(dbUrl, port);

            // 3. Wait for /health to come up.
            BaseUri = new Uri($"http://127.0.0.1:{port}");
            var ready = await WaitForHealthyAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (!ready)
            {
                UnavailableReason = "external API did not become healthy within 30s";
                return;
            }

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            UnavailableReason = $"external API unavailable: {ex.Message}";
        }
    }

    private async Task<string?> ProvisionKeyAsync(string dbUrl)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "uv",
            ArgumentList = { "run", "lockoverseer-mockapi", "keys", "new", "--label", "integration" },
            WorkingDirectory = _workDir!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["LOCKOVERSEER_DATABASE_URL"] = dbUrl;

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await proc.WaitForExitAsync().ConfigureAwait(false);

        if (proc.ExitCode != 0)
        {
            UnavailableReason = $"keys new exited {proc.ExitCode}: {stderr}";
            return null;
        }

        foreach (var line in stdout.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("key: "))
                return trimmed["key: ".Length..].Trim();
        }
        return null;
    }

    private Process StartServer(string dbUrl, int port)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "uv",
            ArgumentList = { "run", "lockoverseer-mockapi", "serve" },
            WorkingDirectory = _workDir!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["LOCKOVERSEER_DATABASE_URL"] = dbUrl;
        psi.Environment["LOCKOVERSEER_BIND_HOST"] = "127.0.0.1";
        psi.Environment["LOCKOVERSEER_BIND_PORT"] = port.ToString();
        psi.Environment["LOCKOVERSEER_LOG_LEVEL"] = "WARNING";

        var proc = Process.Start(psi)!;
        // Drain stdout/stderr asynchronously to prevent buffer-fill deadlock.
        _ = Task.Run(async () => { try { await proc.StandardOutput.ReadToEndAsync(); } catch { /* ignore */ } });
        _ = Task.Run(async () => { try { await proc.StandardError.ReadToEndAsync(); } catch { /* ignore */ } });
        return proc;
    }

    private async Task<bool> WaitForHealthyAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { BaseAddress = BaseUri, Timeout = TimeSpan.FromSeconds(1) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var r = await http.GetAsync("/health").ConfigureAwait(false);
                if (r.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                // server not up yet
            }
            await Task.Delay(250).ConfigureAwait(false);
        }
        return false;
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public Task DisposeAsync()
    {
        try
        {
            if (_proc is not null && !_proc.HasExited)
            {
                _proc.Kill(entireProcessTree: true);
                _proc.WaitForExit(5000);
            }
            _proc?.Dispose();
        }
        catch { /* best-effort */ }

        try
        {
            if (_tempDir is not null && Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* best-effort */ }

        return Task.CompletedTask;
    }
}
