using Tinkwell;
using Tinkwell.Runner;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class RunnerOptionsTests
{
    [Fact]
    public void Parse_ValidArgs_ReturnsOptions()
    {
        var id = ShortIdGenerator.NewId();
        var args = new[]
        {
            "--runner-id", id,
            "--coordinator-pipe", "tinkwell-coordinator",
            "--sentinel-pipe", "tinkwell-coordinator-sentinel"
        };

        var options = RunnerOptions.Parse(args);

        Assert.Equal(id, options.RunnerId);
        Assert.Equal("tinkwell-coordinator", options.CoordinatorPipe);
        Assert.Equal("tinkwell-coordinator-sentinel", options.SentinelPipe);
    }

    [Fact]
    public void Parse_ArgsInterspersedWithOthers_StillWorks()
    {
        var id = ShortIdGenerator.NewId();
        var args = new[]
        {
            "--some-other-flag", "value",
            "--runner-id", id,
            "--coordinator-pipe", "pipe",
            "--sentinel-pipe", "sentinel"
        };

        var options = RunnerOptions.Parse(args);

        Assert.Equal(id, options.RunnerId);
        Assert.Equal("pipe", options.CoordinatorPipe);
        Assert.Equal("sentinel", options.SentinelPipe);
    }

    [Fact]
    public void Parse_MissingRunnerId_Throws()
    {
        var args = new[] { "--coordinator-pipe", "pipe", "--sentinel-pipe", "sentinel" };

        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(args));
    }

    [Fact]
    public void Parse_InvalidId_Throws()
    {
        var args = new[]
        {
            "--runner-id", "not-valid",
            "--coordinator-pipe", "pipe",
            "--sentinel-pipe", "sentinel"
        };

        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(args));
    }

    [Fact]
    public void Parse_MissingCoordinatorPipe_Throws()
    {
        var args = new[]
        {
            "--runner-id", ShortIdGenerator.NewId(),
            "--sentinel-pipe", "sentinel"
        };

        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(args));
    }

    [Fact]
    public void Parse_MissingSentinelPipe_Throws()
    {
        var args = new[]
        {
            "--runner-id", ShortIdGenerator.NewId(),
            "--coordinator-pipe", "pipe"
        };

        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(args));
    }

    [Fact]
    public void Parse_EmptyArgs_Throws()
    {
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse([]));
    }
}
