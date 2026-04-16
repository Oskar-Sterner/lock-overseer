using System.Collections.Generic;
using LockOverseer.Api;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class SseFrameParserTests
{
    [Fact]
    public void Single_complete_frame_yields_one_SseFrame()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed("id: 1\nevent: ban.created\ndata: {\"steam_id\":42}\n\n", frames);

        frames.Count.ShouldBe(1);
        frames[0].Id.ShouldBe(1L);
        frames[0].Event.ShouldBe("ban.created");
        frames[0].Data.ShouldBe("{\"steam_id\":42}");
    }

    [Fact]
    public void Multiple_frames_in_one_feed_yield_each_in_order()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed("id: 1\nevent: a\ndata: {}\n\nid: 2\nevent: b\ndata: {}\n\n", frames);
        frames.Count.ShouldBe(2);
        frames[0].Event.ShouldBe("a");
        frames[1].Event.ShouldBe("b");
    }

    [Fact]
    public void Frame_split_across_two_feed_calls_is_reassembled()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed("id: 5\nevent: ba", frames);
        parser.Feed("n.created\ndata: {}\n\n", frames);
        frames.Count.ShouldBe(1);
        frames[0].Id.ShouldBe(5L);
        frames[0].Event.ShouldBe("ban.created");
    }

    [Fact]
    public void Comment_only_frames_are_ignored()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed(":hb\n\n:hb\n\nid: 1\nevent: x\ndata: {}\n\n", frames);
        frames.Count.ShouldBe(1);
    }

    [Fact]
    public void Frame_without_id_yields_frame_with_null_id()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed("event: x\ndata: {}\n\n", frames);
        frames.Count.ShouldBe(1);
        frames[0].Id.ShouldBeNull();
    }

    [Fact]
    public void Unknown_field_lines_are_discarded()
    {
        var parser = new SseFrameParser();
        var frames = new List<SseFrame>();
        parser.Feed("retry: 3000\nid: 1\nevent: x\ndata: {}\n\n", frames);
        frames.Count.ShouldBe(1);
    }
}
