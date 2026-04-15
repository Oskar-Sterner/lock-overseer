using System.Linq;
using System.Reflection;
using LockOverseer.Contracts;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Contracts;

public sealed class InterfaceShapeTests
{
    [Fact]
    public void ILockOverseerService_declares_synchronous_hot_path_reads()
    {
        var t = typeof(ILockOverseerService);
        t.GetMethod("IsBanned")!.ReturnType.ShouldBe(typeof(bool));
        t.GetMethod("IsMuted")!.ReturnType.ShouldBe(typeof(bool));
        t.GetMethod("HasFlag")!.ReturnType.ShouldBe(typeof(bool));
        t.GetMethod("GetRole")!.ReturnType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Write_methods_return_ValueTask_of_Result()
    {
        var m = typeof(ILockOverseerService).GetMethod("IssueBanAsync")!;
        m.ReturnType.IsGenericType.ShouldBeTrue();
        m.ReturnType.GetGenericTypeDefinition().ShouldBe(typeof(System.Threading.Tasks.ValueTask<>));
    }
}
