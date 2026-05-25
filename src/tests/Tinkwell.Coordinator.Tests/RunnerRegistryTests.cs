using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Tests;

public class RunnerRegistryTests
{
    private static RunnerConfig MakeRunnerConfig(string name, params RunletConfig[] runlets) =>
        new(name, $"runners/{name}", new Dictionary<string, ConfigValue>(),
            runlets, new SourceLocation("test.tw", 1, 1));

    private static RunletConfig MakeRunletConfig(string name) =>
        new(name, $"runlets/{name}.dll", new Dictionary<string, ConfigValue>(),
            new SourceLocation("test.tw", 1, 1));

    private static EnsembleConfig MakeEnsemble(params RunnerConfig[] runners) =>
        new(runners);

    [Fact]
    public void Constructor_CreatesOneDefinitionPerRunner()
    {
        var config = MakeEnsemble(
            MakeRunnerConfig("runner-a"),
            MakeRunnerConfig("runner-b"),
            MakeRunnerConfig("runner-c"));

        var registry = new RunnerRegistry(config);

        Assert.Equal(3, registry.All.Count);
    }

    [Fact]
    public void All_PreservesStartupOrder()
    {
        var config = MakeEnsemble(
            MakeRunnerConfig("first"),
            MakeRunnerConfig("second"),
            MakeRunnerConfig("third"));

        var registry = new RunnerRegistry(config);
        var names = registry.All.Select(r => r.Config.Name).ToList();

        Assert.Equal(["first", "second", "third"], names);
    }

    [Fact]
    public void All_ReturnsCopy()
    {
        var config = MakeEnsemble(MakeRunnerConfig("runner-a"));
        var registry = new RunnerRegistry(config);

        var list1 = registry.All;
        var list2 = registry.All;

        Assert.NotSame(list1, list2);
        Assert.Equal(list1.Count, list2.Count);
    }

    [Fact]
    public void FindById_ReturnsCorrectRunner()
    {
        var config = MakeEnsemble(MakeRunnerConfig("a"), MakeRunnerConfig("b"));
        var registry = new RunnerRegistry(config);

        var expected = registry.All[1];
        var found = registry.FindById(expected.Id);

        Assert.NotNull(found);
        Assert.Equal("b", found.Config.Name);
    }

    [Fact]
    public void FindById_ReturnsNull_WhenNotFound()
    {
        var config = MakeEnsemble(MakeRunnerConfig("a"));
        var registry = new RunnerRegistry(config);

        Assert.Null(registry.FindById("000000"));
    }

    [Fact]
    public void FindByName_CaseSensitive()
    {
        var config = MakeEnsemble(MakeRunnerConfig("My-Runner"));
        var registry = new RunnerRegistry(config);

        Assert.NotNull(registry.FindByName("My-Runner"));
        Assert.Null(registry.FindByName("MY-RUNNER"));
        Assert.Null(registry.FindByName("my-runner"));
    }

    [Fact]
    public void FindByName_ReturnsNull_WhenNotFound()
    {
        var config = MakeEnsemble(MakeRunnerConfig("a"));
        var registry = new RunnerRegistry(config);

        Assert.Null(registry.FindByName("nonexistent"));
    }

    [Fact]
    public void Snapshot_ReturnsRunnerInfoForAll()
    {
        var config = MakeEnsemble(
            MakeRunnerConfig("runner-x"),
            MakeRunnerConfig("runner-y"));
        var registry = new RunnerRegistry(config);

        registry.All[0].MarkReady();
        registry.All[1].MarkFatal("oops");

        var snapshot = registry.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("runner-x", snapshot[0].Name);
        Assert.Equal(RunnerStatus.Ready, snapshot[0].Status);
        Assert.Equal("runner-y", snapshot[1].Name);
        Assert.Equal(RunnerStatus.Fatal, snapshot[1].Status);
    }

    [Fact]
    public void FindById_AfterPrepareRestart_FindsByNewId()
    {
        var config = MakeEnsemble(MakeRunnerConfig("runner-a"));
        var registry = new RunnerRegistry(config);

        var runner = registry.All[0];
        var oldId = runner.Id;

        runner.PrepareRestart();

        Assert.Null(registry.FindById(oldId));
        Assert.NotNull(registry.FindById(runner.Id));
    }

    [Fact]
    public void EmptyConfig_ProducesEmptyRegistry()
    {
        var config = MakeEnsemble();
        var registry = new RunnerRegistry(config);

        Assert.Empty(registry.All);
        Assert.Empty(registry.Snapshot());
        Assert.Null(registry.FindById("000000"));
        Assert.Null(registry.FindByName("anything"));
    }
}
