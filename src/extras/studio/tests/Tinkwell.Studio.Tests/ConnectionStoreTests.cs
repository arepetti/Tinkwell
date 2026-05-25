using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class ConnectionStoreTests : IDisposable
{
    private readonly string _filePath;

    public ConnectionStoreTests()
    {
        _filePath = Path.Combine(
            Path.GetTempPath(),
            $"tw-studio-conn-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    [Fact]
    public async Task LoadAsync_returns_LocalDefault_when_file_missing()
    {
        var store = new ConnectionStore(_filePath, NullLogger<ConnectionStore>.Instance);

        var loaded = await store.LoadAsync();

        Assert.Equal(CoordinatorTransport.LocalDefault, loaded.Transport);
        Assert.Null(loaded.PipeName);
        Assert.Null(loaded.Machine);
        Assert.Null(loaded.DockerContainer);
        Assert.False(loaded.UseDockerCompose);
    }

    [Fact]
    public async Task LoadAsync_returns_LocalDefault_when_file_is_corrupt()
    {
        File.WriteAllText(_filePath, "{ this is not json");
        var store = new ConnectionStore(_filePath, NullLogger<ConnectionStore>.Instance);

        var loaded = await store.LoadAsync();

        Assert.Equal(CoordinatorTransport.LocalDefault, loaded.Transport);
    }

    [Theory]
    [InlineData(CoordinatorTransport.LocalDefault, null, null, null, false)]
    [InlineData(CoordinatorTransport.LocalCustomPipe, "my-pipe", null, null, false)]
    [InlineData(CoordinatorTransport.Remote, "tinkwell-coordinator", "server.lan", null, false)]
    [InlineData(CoordinatorTransport.Docker, null, null, "tinkwell", false)]
    [InlineData(CoordinatorTransport.Docker, null, null, "tinkwell", true)]
    public async Task Save_then_load_round_trips_every_transport(
        CoordinatorTransport transport,
        string? pipeName,
        string? machine,
        string? dockerContainer,
        bool useDockerCompose)
    {
        var original = new CoordinatorConnection(transport, pipeName, machine, dockerContainer, useDockerCompose);
        var store = new ConnectionStore(_filePath, NullLogger<ConnectionStore>.Instance);

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync();

        Assert.Equal(original, loaded);
    }

    [Fact]
    public async Task SaveAsync_creates_missing_directories()
    {
        var nested = Path.Combine(
            Path.GetTempPath(),
            $"tw-studio-conn-{Guid.NewGuid():N}",
            "sub",
            "connection.json");
        try
        {
            var store = new ConnectionStore(nested, NullLogger<ConnectionStore>.Instance);
            await store.SaveAsync(CoordinatorConnection.LocalDefault);

            Assert.True(File.Exists(nested));
        }
        finally
        {
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(nested));
            if (dir is not null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
