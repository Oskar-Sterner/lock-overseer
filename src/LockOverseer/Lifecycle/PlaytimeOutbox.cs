using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Lifecycle;

public sealed record PlaytimeEntry(long SteamId, long Seconds);

public sealed class PlaytimeOutbox
{
    private readonly string _path;
    private readonly ILogger<PlaytimeOutbox> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PlaytimeOutbox(string path, ILogger<PlaytimeOutbox> log)
    {
        _path = path; _log = log;
    }

    public async Task EnqueueAsync(long steamId, long seconds, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await LoadAsync(ct).ConfigureAwait(false);
            list.Add(new PlaytimeEntry(steamId, seconds));
            await WriteAsync(list, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PlaytimeEntry>> DrainAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await LoadAsync(ct).ConfigureAwait(false);
            if (list.Count == 0) return System.Array.Empty<PlaytimeEntry>();
            await WriteAsync(new List<PlaytimeEntry>(), ct).ConfigureAwait(false);
            return list;
        }
        finally { _gate.Release(); }
    }

    private async Task<List<PlaytimeEntry>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new List<PlaytimeEntry>();
        try
        {
            await using var fs = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<PlaytimeEntry>>(fs, JsonDefaults.Options, ct).ConfigureAwait(false) ?? new();
        }
        catch (System.Exception ex)
        {
            _log.LogError(ex, "[LockOverseer.Authority] Corrupt outbox at {Path}; resetting", _path);
            return new List<PlaytimeEntry>();
        }
    }

    private async Task WriteAsync(List<PlaytimeEntry> list, CancellationToken ct)
    {
        var tmp = _path + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, list, JsonDefaults.Options, ct).ConfigureAwait(false);
        File.Move(tmp, _path, overwrite: true);
    }
}
