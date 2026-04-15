using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Lifecycle;

public sealed class PlaytimeOutboxTests
{
    [Fact]
    public async Task Enqueue_persists_to_disk_and_is_read_back_on_restart()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lo_outbox_{System.Guid.NewGuid():N}.json");
        try
        {
            var a = new PlaytimeOutbox(tmp, NullLogger<PlaytimeOutbox>.Instance);
            await a.EnqueueAsync(1, 120, CancellationToken.None);
            await a.EnqueueAsync(2, 45, CancellationToken.None);

            var b = new PlaytimeOutbox(tmp, NullLogger<PlaytimeOutbox>.Instance);
            var drained = await b.DrainAsync(CancellationToken.None);
            drained.Count.ShouldBe(2);
            drained[0].SteamId.ShouldBe(1);
            drained[0].Seconds.ShouldBe(120);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public async Task DrainAsync_clears_file_when_complete()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lo_outbox_{System.Guid.NewGuid():N}.json");
        try
        {
            var o = new PlaytimeOutbox(tmp, NullLogger<PlaytimeOutbox>.Instance);
            await o.EnqueueAsync(1, 1, CancellationToken.None);
            await o.DrainAsync(CancellationToken.None);
            (await o.DrainAsync(CancellationToken.None)).Count.ShouldBe(0);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
