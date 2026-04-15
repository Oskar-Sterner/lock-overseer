using LockOverseer.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace LockOverseer.IntegrationTests;

public sealed class MockApiContainerSmokeTests : IClassFixture<MockApiContainerFixture>
{
    private readonly MockApiContainerFixture _fx;
    public MockApiContainerSmokeTests(MockApiContainerFixture fx) => _fx = fx;

    [Fact]
    public async Task Health_endpoint_is_reachable()
    {
        if (!_fx.DockerAvailable)
        {
            // Docker daemon is not reachable from this environment — skip rather than fail.
            // The CI environment must provide Docker to exercise this test.
            return;
        }

        using var http = new HttpClient { BaseAddress = _fx.BaseUri };
        var r = await http.GetAsync("/health");
        ((int)r.StatusCode).ShouldBe(200);
    }
}
