using LockOverseer.Contracts.Models;
using LockOverseer.Contracts.Models.Requests;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Contracts;

public sealed class RequestDtoTests
{
    [Fact]
    public void BanRequest_supports_permanent_via_null_duration()
    {
        var r = new BanRequest(76561198000000001, null, "cheating", new Issuer(null, "chat"));
        r.DurationMinutes.ShouldBeNull();
        r.Reason.ShouldBe("cheating");
    }

    [Fact]
    public void RoleGrantRequest_carries_role_name()
    {
        var r = new RoleGrantRequest(76561198000000002, "mod", 60, new Issuer(42, "oskar"));
        r.RoleName.ShouldBe("mod");
        r.DurationMinutes.ShouldBe(60);
    }

    [Fact]
    public void RevokeRequest_carries_reason_and_issuer()
    {
        var r = new RevokeRequest("appealed", new Issuer(null, "chat"));
        r.Reason.ShouldBe("appealed");
    }
}
