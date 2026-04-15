using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Contracts;
using LockOverseer.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Lifecycle;

public sealed class PlaytimeTrackerTests
{
    [Fact]
    public async Task Disconnect_without_API_enqueues_to_outbox()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lo_pt_{Guid.NewGuid():N}.json");
        try
        {
            var client = Substitute.For<IAuthorityClient>();
            client.AddPlaytimeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                  .Returns(Result<PlayerResource>.Fail(new AuthorityError(AuthorityErrorKind.Unreachable, "down")));

            var outbox = new PlaytimeOutbox(tmp, NullLogger<PlaytimeOutbox>.Instance);
            var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-04-15T00:00:00Z"));
            var sut = new PlaytimeTracker(client, outbox, time, NullLogger<PlaytimeTracker>.Instance);

            sut.StartSession(42);
            time.Advance(TimeSpan.FromSeconds(75));
            await sut.EndSessionAsync(42, CancellationToken.None);

            var drained = await outbox.DrainAsync(CancellationToken.None);
            drained.Count.ShouldBe(1);
            drained[0].SteamId.ShouldBe(42);
            drained[0].Seconds.ShouldBe(75);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public async Task ReplayOutboxAsync_calls_API_for_each_pending_entry()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lo_pt_{Guid.NewGuid():N}.json");
        try
        {
            var outbox = new PlaytimeOutbox(tmp, NullLogger<PlaytimeOutbox>.Instance);
            await outbox.EnqueueAsync(1, 10, CancellationToken.None);
            await outbox.EnqueueAsync(2, 20, CancellationToken.None);

            var client = Substitute.For<IAuthorityClient>();
            client.AddPlaytimeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                  .Returns(Result<PlayerResource>.Ok(new PlayerResource(1, null, null, null, 0)));

            var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var sut = new PlaytimeTracker(client, outbox, time, NullLogger<PlaytimeTracker>.Instance);

            await sut.ReplayOutboxAsync(CancellationToken.None);
            await client.Received(1).AddPlaytimeAsync(1, 10, Arg.Any<CancellationToken>());
            await client.Received(1).AddPlaytimeAsync(2, 20, Arg.Any<CancellationToken>());
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
