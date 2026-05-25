namespace Tinkwell.Integration.Tests;

[Trait("Category", "Integration")]
public class SmokeTests : IAsyncLifetime
{
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tw-smoke-{Guid.NewGuid():N}");
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
    public async Task TwoHeadlessRunners_NoRunlets_ExitAfterInit()
    {
        var configPath = WriteConfig("""
            runner headless-a from "Tinkwell.Runner.Headless.dll" ;
            runner headless-b from "Tinkwell.Runner.Headless.dll" ;
            """);

        var pipeName = CoordinatorProcess.UniquePipeName();

        await using var coordinator = CoordinatorProcess.Start(
            configPath,
            pipeName,
            "--Coordinator:ExitAfterInit=true",
            "--Coordinator:ReadyTimeoutSeconds=15");

        var exitCode = await coordinator.WaitForExitAsync(TimeSpan.FromSeconds(30));

        var output = coordinator.CombinedOutput;
        Assert.True(exitCode == 0,
            $"Coordinator exited with code {exitCode}.\n--- Output ---\n{output}");
        Assert.Contains("startup sequence complete", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reported ready", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SingleRunner_WithTestRunlet_ReadyMode()
    {
        var configPath = WriteConfig("""
            runner headless-a from "Tinkwell.Runner.Headless.dll" {
                runlet test-a from "Tinkwell.Runlet.Test.dll" {
                    mode = "ready"
                }
            }
            """);

        var pipeName = CoordinatorProcess.UniquePipeName();

        await using var coordinator = CoordinatorProcess.Start(
            configPath,
            pipeName,
            "--Coordinator:ExitAfterInit=true",
            "--Coordinator:ReadyTimeoutSeconds=15");

        var exitCode = await coordinator.WaitForExitAsync(TimeSpan.FromSeconds(30));

        var output = coordinator.CombinedOutput;
        Assert.True(exitCode == 0,
            $"Coordinator exited with code {exitCode}.\n--- Output ---\n{output}");
        Assert.Contains("startup sequence complete", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reported ready", output, StringComparison.OrdinalIgnoreCase);
    }
}
