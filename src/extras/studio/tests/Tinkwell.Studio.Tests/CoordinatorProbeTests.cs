using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class CoordinatorProbeTests
{
    [Fact]
    public void BuildCommand_LocalDefault_uses_TwExecutablePath()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var (file, args) = probe.BuildCommand(CoordinatorConnection.LocalDefault);

        Assert.Equal("tw", file);
        Assert.Contains("ping", args);
        Assert.DoesNotContain("--pipe", args);
        Assert.DoesNotContain("--machine", args);
    }

    [Fact]
    public void BuildCommand_LocalCustomPipe_includes_pipe_flag()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var connection = new CoordinatorConnection(
            CoordinatorTransport.LocalCustomPipe, "lab-pipe", null, null, false);

        var (file, args) = probe.BuildCommand(connection);

        Assert.Equal("tw", file);
        Assert.Contains("--pipe", args);
        Assert.Contains("lab-pipe", args);
        Assert.DoesNotContain("--machine", args);
    }

    [Fact]
    public void BuildCommand_Remote_includes_pipe_and_machine()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var connection = new CoordinatorConnection(
            CoordinatorTransport.Remote, "lab-pipe", "server.lan", null, false);

        var (file, args) = probe.BuildCommand(connection);

        Assert.Equal("tw", file);
        Assert.Contains("--pipe", args);
        Assert.Contains("lab-pipe", args);
        Assert.Contains("--machine", args);
        Assert.Contains("server.lan", args);
    }

    [Fact]
    public void BuildCommand_Docker_wraps_in_docker_exec()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var connection = new CoordinatorConnection(
            CoordinatorTransport.Docker, null, null, "tinkwell", UseDockerCompose: false);

        var (file, args) = probe.BuildCommand(connection);

        Assert.Equal("docker", file);
        Assert.Equal("exec", args[0]);
        Assert.Equal("tinkwell", args[1]);
        Assert.Equal("tw", args[2]);
        Assert.Equal("ping", args[3]);
    }

    [Fact]
    public void BuildCommand_Docker_with_compose_inserts_compose_subcommand()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var connection = new CoordinatorConnection(
            CoordinatorTransport.Docker, null, null, "tinkwell", UseDockerCompose: true);

        var (file, args) = probe.BuildCommand(connection);

        Assert.Equal("docker", file);
        Assert.Equal("compose", args[0]);
        Assert.Equal("exec", args[1]);
        Assert.Equal("tinkwell", args[2]);
        Assert.Equal("tw", args[3]);
        Assert.Equal("ping", args[4]);
    }

    [Fact]
    public async Task PingAsync_returns_Ok_when_stub_succeeds()
    {
        var stub = StubCli.Create(new[] { "{\"pong\":true}" });
        var settings = new StudioSettings { TwExecutablePath = stub };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var result = await probe.PingAsync(CoordinatorConnection.LocalDefault);

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task PingAsync_returns_Failed_with_stderr_when_stub_exits_nonzero()
    {
        var stub = StubCli.Create(
            stdoutLines: Array.Empty<string>(),
            exitCode: 1,
            stderr: "coordinator unreachable");
        var settings = new StudioSettings { TwExecutablePath = stub };
        var probe = new CoordinatorProbe(settings, NullLogger<CoordinatorProbe>.Instance);

        var result = await probe.PingAsync(CoordinatorConnection.LocalDefault);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("coordinator unreachable", result.Error);
    }
}
