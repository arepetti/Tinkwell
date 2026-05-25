using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class TwCliProcessRunnerDockerTests
{
    [Fact]
    public void Without_docker_container_uses_TwExecutablePath_directly()
    {
        var settings = new StudioSettings { TwExecutablePath = "tw" };
        var runner = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);

        var (file, args) = runner.ResolveCommand(new[] { "ping" });

        Assert.Equal("tw", file);
        Assert.Equal(new[] { "ping" }, args);
    }

    [Fact]
    public void Docker_container_only_wraps_call_with_docker_exec()
    {
        var settings = new StudioSettings
        {
            TwExecutablePath = "tw",
            DockerContainer = "tinkwell",
            UseDockerCompose = false,
        };
        var runner = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);

        var (file, args) = runner.ResolveCommand(new[] { "ping", "--format", "jsonl" });

        Assert.Equal("docker", file);
        Assert.Equal(new[] { "exec", "tinkwell", "tw", "ping", "--format", "jsonl" }, args);
    }

    [Fact]
    public void Docker_compose_mode_prepends_compose_subcommand()
    {
        var settings = new StudioSettings
        {
            TwExecutablePath = "tw",
            DockerContainer = "tinkwell",
            UseDockerCompose = true,
        };
        var runner = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);

        var (file, args) = runner.ResolveCommand(new[] { "ping" });

        Assert.Equal("docker", file);
        Assert.Equal(new[] { "compose", "exec", "tinkwell", "tw", "ping" }, args);
    }

    [Fact]
    public void BuildArgs_appends_pipe_machine_and_format_flags()
    {
        var settings = new StudioSettings
        {
            TwExecutablePath = "tw",
            PipeName = "lab-pipe",
            Machine = "server.lan",
        };
        var runner = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);

        var args = runner.BuildArgs(new[] { "ping" });

        Assert.Contains("--pipe", args);
        Assert.Contains("lab-pipe", args);
        Assert.Contains("--machine", args);
        Assert.Contains("server.lan", args);
        Assert.Contains("--format", args);
        Assert.Contains("jsonl", args);
        Assert.Contains("--non-interactive", args);
    }

    [Fact]
    public void BuildArgs_skips_pipe_and_machine_when_unset()
    {
        // Docker mode clears PipeName / Machine via StudioSettings.Apply because
        // the call is wrapped in `docker exec`; the args targeting `tw` inside
        // the container should be the bare ping command + format flags.
        var settings = new StudioSettings
        {
            TwExecutablePath = "tw",
            DockerContainer = "tinkwell",
        };
        var runner = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);

        var args = runner.BuildArgs(new[] { "ping" });

        Assert.DoesNotContain("--pipe", args);
        Assert.DoesNotContain("--machine", args);
        Assert.Contains("--format", args);
        Assert.Contains("jsonl", args);
    }

    [Fact]
    public void StudioSettings_Apply_with_docker_connection_clears_pipe_and_machine()
    {
        var settings = new StudioSettings
        {
            PipeName = "leftover",
            Machine = "leftover",
            DockerContainer = null,
        };

        settings.Apply(new CoordinatorConnection(
            CoordinatorTransport.Docker, "ignored", "ignored", "tinkwell", UseDockerCompose: true));

        Assert.Null(settings.PipeName);
        Assert.Null(settings.Machine);
        Assert.Equal("tinkwell", settings.DockerContainer);
        Assert.True(settings.UseDockerCompose);
    }

    [Fact]
    public void StudioSettings_Apply_with_remote_connection_sets_pipe_and_machine()
    {
        var settings = new StudioSettings
        {
            DockerContainer = "leftover",
            UseDockerCompose = true,
        };

        settings.Apply(new CoordinatorConnection(
            CoordinatorTransport.Remote, "lab-pipe", "server.lan", null, false));

        Assert.Equal("lab-pipe", settings.PipeName);
        Assert.Equal("server.lan", settings.Machine);
        Assert.Null(settings.DockerContainer);
        Assert.False(settings.UseDockerCompose);
    }

    [Fact]
    public void StudioSettings_Apply_with_local_default_clears_everything()
    {
        var settings = new StudioSettings
        {
            PipeName = "leftover",
            Machine = "leftover",
            DockerContainer = "leftover",
            UseDockerCompose = true,
        };

        settings.Apply(CoordinatorConnection.LocalDefault);

        Assert.Null(settings.PipeName);
        Assert.Null(settings.Machine);
        Assert.Null(settings.DockerContainer);
        Assert.False(settings.UseDockerCompose);
    }
}
