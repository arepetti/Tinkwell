using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Tests;

/// <summary>
/// Tests for the restart policy logic as implemented via
/// <see cref="RunnerState.CrashesInWindow"/> and
/// <see cref="RunnerState.RecordCrash"/>.
/// The <see cref="RunnerMonitor"/> itself calls these methods
/// and compares against <see cref="RestartPolicyOptions"/>.
/// </summary>
public class RunnerMonitorTests
{
    private static RunnerState MakeRunner(string name = "test-runner") =>
        new(new RunnerConfig(
            name, "runners/Test",
            new Dictionary<string, ConfigValue>(),
            Array.Empty<RunletConfig>(),
            new SourceLocation("test.tw", 1, 1)));

    [Fact]
    public void CrashesInWindow_NoCrashes_ReturnsZero()
    {
        var runner = MakeRunner();
        Assert.Equal(0, runner.CrashesInWindow(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void CrashesInWindow_AllRecent_ReturnsAll()
    {
        var runner = MakeRunner();

        runner.RecordCrash();
        runner.RecordCrash();
        runner.RecordCrash();

        Assert.Equal(3, runner.CrashesInWindow(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void RestartPolicy_WithinLimit_WouldAllowRestart()
    {
        var runner = MakeRunner();
        int maxRestarts = 3;
        var window = TimeSpan.FromMinutes(1);

        runner.RecordCrash();
        runner.RecordCrash();

        Assert.True(runner.CrashesInWindow(window) < maxRestarts);
    }

    [Fact]
    public void RestartPolicy_AtLimit_WouldDenyRestart()
    {
        var runner = MakeRunner();
        int maxRestarts = 3;
        var window = TimeSpan.FromMinutes(1);

        runner.RecordCrash();
        runner.RecordCrash();
        runner.RecordCrash();

        Assert.True(runner.CrashesInWindow(window) >= maxRestarts);
    }

    [Fact]
    public void RestartPolicy_AboveLimit_WouldDenyRestart()
    {
        var runner = MakeRunner();
        int maxRestarts = 2;
        var window = TimeSpan.FromMinutes(1);

        runner.RecordCrash();
        runner.RecordCrash();
        runner.RecordCrash();

        Assert.True(runner.CrashesInWindow(window) >= maxRestarts);
    }

    [Fact]
    public void RecordCrash_ThenPrepareRestart_StatusIsRestarting()
    {
        var runner = MakeRunner();
        runner.RecordCrash();

        Assert.Equal(RunnerStatus.Crashed, runner.Status);

        runner.PrepareRestart();
        Assert.Equal(RunnerStatus.Restarting, runner.Status);
        Assert.Equal(1, runner.RestartCount);
    }

    [Fact]
    public void RecordCrash_DoesNotResetRestartCount()
    {
        var runner = MakeRunner();

        runner.PrepareRestart();
        runner.PrepareRestart();
        Assert.Equal(2, runner.RestartCount);

        runner.RecordCrash();
        Assert.Equal(2, runner.RestartCount);
    }

    [Fact]
    public void MarkFatal_PreventsSubsequentRestartAttempt()
    {
        var runner = MakeRunner();
        runner.MarkFatal("Restart limit exceeded");

        Assert.Equal(RunnerStatus.Fatal, runner.Status);
        Assert.Equal("Restart limit exceeded", runner.FatalMessage);
    }

    [Fact]
    public void CrashTimestamps_ArePreservedAcrossRestarts()
    {
        var runner = MakeRunner();

        runner.RecordCrash();
        runner.PrepareRestart();
        runner.RecordCrash();

        Assert.Equal(2, runner.CrashTimestamps.Count);
        Assert.Equal(2, runner.CrashesInWindow(TimeSpan.FromMinutes(1)));
    }
}
