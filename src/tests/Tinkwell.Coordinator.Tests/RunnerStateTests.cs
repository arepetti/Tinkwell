using Tinkwell;
using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Tests;

public class RunnerStateTests
{
    private static RunnerConfig MakeConfig(string name = "test-runner") =>
        new(name, "runners/TestRunner", new Dictionary<string, ConfigValue>(),
            Array.Empty<RunletConfig>(), new SourceLocation("test.tw", 1, 1));

    private static ServiceDefinition Service(string name) =>
        new(
            name,
            ServiceType.Grpc,
            null,
            "test",
            Array.Empty<string>(),
            "127.0.0.1:1",
            $"http://127.0.0.1:1/{name}");

    [Fact]
    public void Constructor_AssignsIdAndStartingStatus()
    {
        var def = new RunnerState(MakeConfig());

        Assert.True(ShortIdGenerator.IsValid(def.Id));
        Assert.Equal(RunnerStatus.Starting, def.Status);
        Assert.Equal("test-runner", def.Config.Name);
        Assert.Null(def.Process);
        Assert.Equal(0, def.RestartCount);
        Assert.Null(def.FatalMessage);
    }

    [Fact]
    public void MarkWaitingForReady_SetsStatus()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkWaitingForReady();
        Assert.Equal(RunnerStatus.WaitingForReady, def.Status);
    }

    [Fact]
    public void MarkReady_SetsStatus()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkReady();
        Assert.Equal(RunnerStatus.Ready, def.Status);
    }

    [Fact]
    public void MarkUnblocked_SetsStatus()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkUnblocked();
        Assert.Equal(RunnerStatus.Unblocked, def.Status);
    }

    [Fact]
    public void MarkCrashed_SetsStatusAndClearsProcess()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkCrashed();
        Assert.Equal(RunnerStatus.Crashed, def.Status);
        Assert.Null(def.Process);
    }

    [Fact]
    public void MarkFatal_SetsStatusAndMessage()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkFatal("runlet mismatch");
        Assert.Equal(RunnerStatus.Fatal, def.Status);
        Assert.Equal("runlet mismatch", def.FatalMessage);
    }

    [Fact]
    public void PrepareRestart_GeneratesNewId_IncrementsCount()
    {
        var def = new RunnerState(MakeConfig());
        var originalId = def.Id;

        def.PrepareRestart();

        Assert.NotEqual(originalId, def.Id);
        Assert.True(ShortIdGenerator.IsValid(def.Id));
        Assert.Equal(1, def.RestartCount);
        Assert.Equal(RunnerStatus.Restarting, def.Status);
        Assert.Null(def.Process);
    }

    [Fact]
    public void PrepareRestart_CalledMultipleTimes_IncrementsEachTime()
    {
        var def = new RunnerState(MakeConfig());

        def.PrepareRestart();
        def.PrepareRestart();
        def.PrepareRestart();

        Assert.Equal(3, def.RestartCount);
    }

    [Fact]
    public void RecordCrash_SetsStatusAndAddsTimestamp()
    {
        var def = new RunnerState(MakeConfig());
        Assert.Empty(def.CrashTimestamps);

        def.RecordCrash();

        Assert.Equal(RunnerStatus.Crashed, def.Status);
        Assert.Single(def.CrashTimestamps);
        Assert.True((DateTimeOffset.UtcNow - def.CrashTimestamps[0]).Duration() < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordCrash_AccumulatesTimestamps()
    {
        var def = new RunnerState(MakeConfig());

        def.RecordCrash();
        def.RecordCrash();
        def.RecordCrash();

        Assert.Equal(3, def.CrashTimestamps.Count);
    }

    [Fact]
    public void CrashesInWindow_CountsRecentCrashesOnly()
    {
        var def = new RunnerState(MakeConfig());

        def.RecordCrash();
        def.RecordCrash();

        var count = def.CrashesInWindow(TimeSpan.FromMinutes(1));
        Assert.Equal(2, count);
    }

    [Fact]
    public void ToInfo_ProducesCorrectSnapshot()
    {
        var def = new RunnerState(MakeConfig("my-runner"));
        def.MarkReady();

        var info = def.ToInfo();

        Assert.Equal("my-runner", info.Name);
        Assert.Equal(def.Id, info.Id);
        Assert.Null(info.ProcessId);
        Assert.Equal(RunnerStatus.Ready, info.Status);
        Assert.Null(info.StartupTime);
    }

    [Fact]
    public void StartupTime_IsNull_BeforeSignalReady()
    {
        var def = new RunnerState(MakeConfig());
        Assert.Null(def.StartupTime);
    }

    [Fact]
    public async Task SignalReady_RecordsStartupTime()
    {
        var def = new RunnerState(MakeConfig());
        def.SetProcess(System.Diagnostics.Process.GetCurrentProcess());
        def.MarkWaitingForReady();

        await Task.Delay(20);
        def.SignalReady();

        Assert.NotNull(def.StartupTime);
        Assert.True(def.StartupTime.Value >= TimeSpan.FromMilliseconds(10));
        Assert.NotNull(def.ToInfo().StartupTime);
    }

    [Fact]
    public async Task SignalReady_CompletesWait_WithReadyResult()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkWaitingForReady();

        var waitTask = def.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        def.SignalReady();

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Ready, result);
        Assert.Equal(RunnerStatus.Ready, def.Status);
    }

    [Fact]
    public async Task SignalFatal_CompletesWait_WithFatalResult()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkWaitingForReady();

        var waitTask = def.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        def.SignalFatal("something broke");

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Fatal, result);
        Assert.Equal(RunnerStatus.Fatal, def.Status);
        Assert.Equal("something broke", def.FatalMessage);
    }

    [Fact]
    public async Task SignalUnblock_CompletesWait_WithUnblockedResult()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkWaitingForReady();

        var waitTask = def.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        def.SignalUnblock();

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Unblocked, result);
        Assert.Equal(RunnerStatus.Unblocked, def.Status);
    }

    [Fact]
    public async Task WaitForReadyAsync_TimesOut_ReturnsTimedOut()
    {
        var def = new RunnerState(MakeConfig());
        def.MarkWaitingForReady();

        var result = await def.WaitForReadyAsync(
            TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Equal(ReadySignalResult.TimedOut, result);
    }

    [Fact]
    public async Task WaitForReadyAsync_CleansUpTcs_AfterCompletion()
    {
        var def = new RunnerState(MakeConfig());

        var wait1 = def.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        def.SignalReady();
        await wait1;

        var wait2 = def.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        def.SignalReady();
        var result = await wait2;

        Assert.Equal(ReadySignalResult.Ready, result);
    }

    [Fact]
    public void Services_ReturnsStableSnapshot_WhenServicesAreReplaced()
    {
        var def = new RunnerState(MakeConfig());
        def.SetServices(new[] { Service("first") });

        var snapshot = def.Services;
        def.SetServices(new[] { Service("second") });

        Assert.Single(snapshot);
        Assert.Equal("first", snapshot[0].Name);
        Assert.Single(def.Services);
        Assert.Equal("second", def.Services[0].Name);
    }

    [Fact]
    public void Services_ReturnsStableSnapshot_WhenServicesAreCleared()
    {
        var def = new RunnerState(MakeConfig());
        def.SetServices(new[] { Service("first") });

        var snapshot = def.Services;
        def.ClearServices();

        Assert.Single(snapshot);
        Assert.Equal("first", snapshot[0].Name);
        Assert.Empty(def.Services);
    }
}
