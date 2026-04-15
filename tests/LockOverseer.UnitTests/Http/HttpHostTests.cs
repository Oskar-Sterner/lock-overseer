using LockOverseer.Http;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Http;

public sealed class HttpHostTests
{
    [Fact]
    public async Task Start_then_stop_completes_without_throwing()
    {
        var host = new HttpHost(new HttpHostOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            Port = 0, // OS-assigned
            DocsEnabled = false
        }, configure: _ => { });

        await ((IHostedService)host).StartAsync(CancellationToken.None);
        host.BoundPort.ShouldBeGreaterThan(0);
        await ((IHostedService)host).StopAsync(CancellationToken.None);
    }
}
