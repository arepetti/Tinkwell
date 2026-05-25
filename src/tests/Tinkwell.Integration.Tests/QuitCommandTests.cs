using System.Text.Json;

namespace Tinkwell.Integration.Tests;

[Trait("Category", "Integration")]
public class QuitCommandTests : IAsyncLifetime
{
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tw-quit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch
        {
        }
        return Task.CompletedTask;
    }

    private string WriteConfig(string content)
    {
        var path = Path.Combine(_tempDir, "test.tw");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Quit_ShutdownCoordinatorGracefully()
    {
        var pipeName = CoordinatorProcess.UniquePipeName();
        var configPath = WriteConfig("""
            runner headless-a from "Tinkwell.Runner.Headless.dll" ;
            """);

        await using var coordinator = CoordinatorProcess.Start(
            configPath, pipeName,
            "--Coordinator:ReadyTimeoutSeconds=15");

        // Wait for startup
        await WaitForRunnerReadyAsync(coordinator, pipeName, TimeSpan.FromSeconds(20));

        // Send quit
        var quitResponse = await coordinator.SendCommandAsync("quit");
        Assert.Equal("ok", quitResponse.GetProperty("status").GetString());

        // Coordinator should exit gracefully
        var exitCode = await coordinator.WaitForExitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Quit_RespondsWithShuttingDown()
    {
        var pipeName = CoordinatorProcess.UniquePipeName();
        var configPath = WriteConfig("""
            runner headless-a from "Tinkwell.Runner.Headless.dll" ;
            """);

        await using var coordinator = CoordinatorProcess.Start(
            configPath, pipeName,
            "--Coordinator:ReadyTimeoutSeconds=15");

        await WaitForRunnerReadyAsync(coordinator, pipeName, TimeSpan.FromSeconds(20));

        var raw = await coordinator.SendPipeCommandAsync("quit");
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("shutting down", root.GetProperty("message").GetString());

        await coordinator.WaitForExitAsync(TimeSpan.FromSeconds(15));
    }

    private static async Task WaitForRunnerReadyAsync(
        CoordinatorProcess coordinator, string pipeName, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var response = await coordinator.SendCommandAsync(
                    "runners list", cts.Token);

                if (response.GetProperty("status").GetString() == "ok")
                    return;
            }
            catch when (!cts.IsCancellationRequested)
            {
            }

            await Task.Delay(500, cts.Token);
        }

        throw new TimeoutException(
            $"Runner did not become ready within {timeout}.\n" +
            $"Coordinator output:\n{coordinator.CombinedOutput}");
    }
}
