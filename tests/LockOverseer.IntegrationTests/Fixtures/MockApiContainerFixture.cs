using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Xunit;

namespace LockOverseer.IntegrationTests.Fixtures;

/// <summary>
/// Spins up the MockAPI container (builds from MockAPI/Dockerfile) for integration tests.
/// Requires a running Docker daemon accessible to the current user; individual tests should
/// use <see cref="SkipIfDockerUnavailable"/> or catch <see cref="DockerUnavailableException"/>
/// if Docker is not reachable in the environment.
/// </summary>
public sealed class MockApiContainerFixture : IAsyncLifetime
{
    private IContainer? _container;
    private IFutureDockerImage? _image;

    public Uri BaseUri { get; private set; } = null!;
    public string ApiKey { get; private set; } = "integration-test-key";
    public bool DockerAvailable { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            var dockerfileDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "..", "MockAPI"));

            if (!File.Exists(Path.Combine(dockerfileDir, "Dockerfile")))
            {
                UnavailableReason = $"MockAPI Dockerfile not found at {dockerfileDir}";
                return;
            }

            _image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(dockerfileDir)
                .WithDockerfile("Dockerfile")
                .Build();
            await _image.CreateAsync();

            _container = new ContainerBuilder()
                .WithImage(_image)
                .WithPortBinding(8080, assignRandomHostPort: true)
                .WithEnvironment("LOCKOVERSEER_BIND_PORT", "8080")
                .WithEnvironment("LOCKOVERSEER_DATABASE_URL", "sqlite+aiosqlite:///:memory:")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(8080)))
                .Build();
            await _container.StartAsync();

            BaseUri = new Uri($"http://localhost:{_container.GetMappedPublicPort(8080)}");
            DockerAvailable = true;
        }
        catch (Exception ex)
        {
            DockerAvailable = false;
            UnavailableReason = $"Docker unavailable: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
        if (_image is not null)
            await _image.DisposeAsync();
    }
}
