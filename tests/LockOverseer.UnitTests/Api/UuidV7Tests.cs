using LockOverseer.Api;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Api;

public sealed class UuidV7Tests
{
    [Fact]
    public void Generates_values_that_are_ordered_by_time()
    {
        var a = UuidV7.NewId();
        System.Threading.Thread.Sleep(2);
        var b = UuidV7.NewId();
        string.CompareOrdinal(a.ToString(), b.ToString()).ShouldBeLessThan(0);
    }

    [Fact]
    public void Version_nibble_is_7()
    {
        var id = UuidV7.NewId();
        var bytes = id.ToByteArray();
        // UUIDv7 layout: version nibble is high 4 bits of byte index 7 in RFC layout.
        // .NET Guid.ToByteArray flips first three fields; version is in byte[7].
        ((bytes[7] & 0xF0) >> 4).ShouldBe(7);
    }
}
