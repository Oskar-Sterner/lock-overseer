using System;
using System.Threading.Tasks;
using LockOverseer.Contracts;
using LockOverseer.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Lifecycle;

public sealed class EnforcementHooksTests
{
    [Fact]
    public void ShouldRejectConnect_true_when_service_reports_banned()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.IsBanned(42).Returns(true);
        var hooks = new EnforcementHooks(svc, NullLogger<EnforcementHooks>.Instance);
        hooks.ShouldRejectConnect(42, out var reason).ShouldBeTrue();
        reason.ShouldContain("banned");
    }

    [Fact]
    public void ShouldSuppressChat_true_when_service_reports_muted()
    {
        var svc = Substitute.For<ILockOverseerService>();
        svc.IsMuted(42).Returns(true);
        var hooks = new EnforcementHooks(svc, NullLogger<EnforcementHooks>.Instance);
        hooks.ShouldSuppressChat(42).ShouldBeTrue();
    }
}
