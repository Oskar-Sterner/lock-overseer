using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using Microsoft.Extensions.Logging;

namespace LockOverseer.Lifecycle;

public sealed class PlaytimeTracker
{
    private readonly IAuthorityClient _client;
    private readonly PlaytimeOutbox _outbox;
    private readonly TimeProvider _time;
    private readonly ILogger<PlaytimeTracker> _log;
    private readonly ConcurrentDictionary<long, DateTimeOffset> _sessions = new();

    public PlaytimeTracker(IAuthorityClient client, PlaytimeOutbox outbox, TimeProvider time, ILogger<PlaytimeTracker> log)
    {
        _client = client; _outbox = outbox; _time = time; _log = log;
    }

    public void StartSession(long steamId) => _sessions[steamId] = _time.GetUtcNow();

    public async Task EndSessionAsync(long steamId, CancellationToken ct)
    {
        if (!_sessions.TryRemove(steamId, out var start)) return;
        var seconds = (long)Math.Round((_time.GetUtcNow() - start).TotalSeconds);
        if (seconds <= 0) return;

        var r = await _client.AddPlaytimeAsync(steamId, seconds, ct).ConfigureAwait(false);
        if (!r.IsSuccess)
        {
            _log.LogWarning("[LockOverseer.Authority] Playtime POST failed for {SteamId}; queued in outbox", steamId);
            await _outbox.EnqueueAsync(steamId, seconds, ct).ConfigureAwait(false);
        }
    }

    public async Task ReplayOutboxAsync(CancellationToken ct)
    {
        var pending = await _outbox.DrainAsync(ct).ConfigureAwait(false);
        foreach (var e in pending)
        {
            var r = await _client.AddPlaytimeAsync(e.SteamId, e.Seconds, ct).ConfigureAwait(false);
            if (!r.IsSuccess)
            {
                await _outbox.EnqueueAsync(e.SteamId, e.Seconds, ct).ConfigureAwait(false);
                _log.LogWarning("[LockOverseer.Authority] Replay still failing for {SteamId}; re-queued", e.SteamId);
            }
        }
    }
}
