using System.Text.Json;
using Grpc.Net.Client;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Integration.Tests;

/// <summary>
/// Shared fixture that launches a coordinator with a gRPC runner hosting
/// the store runlet (in-memory backend). Provides a gRPC client for tests
/// and tears down the coordinator on dispose.
/// </summary>
public sealed class StoreFixture : IAsyncLifetime
{
    private string _tempDir = null!;
    private CoordinatorProcess _coordinator = null!;
    private GrpcChannel _channel = null!;

    public StateStore.StateStoreClient Client { get; private set; } = null!;
    internal CoordinatorProcess Coordinator => _coordinator;
    public string PipeName { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tw-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        PipeName = CoordinatorProcess.UniquePipeName();

        var configPath = WriteConfig("""
            runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
                runlet store from "Tinkwell.Runlet.Store.dll" {
                    storage = "memory"
                    expiration-interval-seconds = 1
                }
            }
            """);

        _coordinator = CoordinatorProcess.Start(
            configPath,
            PipeName,
            "--Coordinator:ReadyTimeoutSeconds=30");

        var storeUrl = await WaitForStoreReadyAsync(TimeSpan.FromSeconds(30));

        _channel = GrpcChannel.ForAddress(storeUrl);
        Client = new StateStore.StateStoreClient(_channel);
    }

    public async Task DisposeAsync()
    {
        _channel?.Dispose();

        if (_coordinator is not null)
        {
            try
            {
                await _coordinator.SendPipeCommandAsync("quit");
                await _coordinator.WaitForExitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
            }

            await _coordinator.DisposeAsync();
        }

        try { Directory.Delete(_tempDir, recursive: true); }
        catch
        {
        }
    }

    private string WriteConfig(string content)
    {
        var path = Path.Combine(_tempDir, "test.tw");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Polls the coordinator until the gRPC runner reports ready and the
    /// store service is registered, then returns the store's gRPC URL.
    /// </summary>
    private async Task<string> WaitForStoreReadyAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await _coordinator.SendCommandAsync("service find store");

                if (response.GetProperty("status").GetString() == "ok" &&
                    response.TryGetProperty("data", out var data))
                {
                    var url = data.GetProperty("url").GetString();
                    if (!string.IsNullOrEmpty(url))
                        return url;
                }
            }
            catch
            {
                // Pipe not ready yet or runner not started
            }

            if (_coordinator.HasExited)
                break;

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Store service did not become available within {timeout}.\n" +
            $"Coordinator output:\n{_coordinator.CombinedOutput}");
    }
}
