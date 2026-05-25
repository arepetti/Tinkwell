using System.Text.RegularExpressions;

namespace Tinkwell.Integration.Tests;

[Trait("Category", "Integration")]
public class SupervisedRestartTests : IAsyncLifetime
{
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tw-restart-{Guid.NewGuid():N}");
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
    public async Task Runner_CrashAfterReady_IsRestartedBySupervisor()
    {
        var pipeName = CoordinatorProcess.UniquePipeName();
        var configPath = WriteConfig("""
            runner crasher from "Tinkwell.Runner.Headless.dll" {
                runlet crash from "Tinkwell.Runlet.Test.dll" {
                    mode = "crash-after-ready"
                    crash-delay-ms = "500"
                }
            }
            """);

        await using var coordinator = CoordinatorProcess.Start(
            configPath, pipeName,
            "--Coordinator:ReadyTimeoutSeconds=20",
            "--Coordinator:RestartPolicy:MaxRestartsInWindow=3",
            "--Coordinator:RestartPolicy:RestartWindowInSeconds=60",
            "--Coordinator:RestartPolicy:QuitOnRunnerCrash=false");

        try
        {
            // Wait long enough for the runner to start, crash, and be restarted
            // at least once. Supervisor + headless runner startup is not instant.
            await WaitForPatternAsync(
                coordinator,
                new Regex(@"Restarting runner 'crasher'", RegexOptions.IgnoreCase),
                TimeSpan.FromSeconds(25));

            await WaitForPatternAsync(
                coordinator,
                new Regex(@"crasher.*reported ready", RegexOptions.IgnoreCase),
                TimeSpan.FromSeconds(25),
                minimumOccurrences: 2);

            Assert.False(coordinator.HasExited,
                $"Coordinator should not exit on runner crash when QuitOnRunnerCrash=false.\n" +
                $"Output:\n{coordinator.CombinedOutput}");
        }
        finally
        {
            try
            {
                await coordinator.SendCommandAsync("quit");
                await coordinator.WaitForExitAsync(TimeSpan.FromSeconds(15));
            }
            catch
            {
                // Best-effort shutdown; DisposeAsync will force-kill if needed.
            }
        }
    }

    private static async Task WaitForPatternAsync(
        CoordinatorProcess coordinator,
        Regex pattern,
        TimeSpan timeout,
        int minimumOccurrences = 1)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (pattern.Matches(coordinator.CombinedOutput).Count >= minimumOccurrences)
                return;

            if (coordinator.HasExited)
                throw new InvalidOperationException(
                    $"Coordinator exited before pattern '{pattern}' matched {minimumOccurrences} time(s).\n" +
                    $"Output:\n{coordinator.CombinedOutput}");

            try { await Task.Delay(250, cts.Token); }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Pattern '{pattern}' did not match {minimumOccurrences} time(s) within {timeout}.\n" +
            $"Coordinator output:\n{coordinator.CombinedOutput}");
    }
}
