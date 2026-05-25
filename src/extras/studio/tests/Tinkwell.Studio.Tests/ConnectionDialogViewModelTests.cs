using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class ConnectionDialogViewModelTests
{
    [Fact]
    public void BuildConnection_for_LocalDefault_emits_LocalDefault_record()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.LocalDefault,
        };

        var built = vm.BuildConnection();

        Assert.Equal(CoordinatorTransport.LocalDefault, built.Transport);
        Assert.Null(built.PipeName);
        Assert.Null(built.Machine);
        Assert.Null(built.DockerContainer);
        Assert.False(built.UseDockerCompose);
    }

    [Fact]
    public void BuildConnection_for_LocalCustomPipe_carries_only_the_pipe_name()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.LocalCustomPipe,
            LocalPipeName = "lab-pipe",
            RemoteMachine = "ignored",
            DockerContainer = "ignored-too",
        };

        var built = vm.BuildConnection();

        Assert.Equal(CoordinatorTransport.LocalCustomPipe, built.Transport);
        Assert.Equal("lab-pipe", built.PipeName);
        Assert.Null(built.Machine);
        Assert.Null(built.DockerContainer);
    }

    [Fact]
    public void BuildConnection_for_Remote_carries_machine_and_pipe()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.Remote,
            RemoteMachine = "  server.lan  ",
            RemotePipeName = "  tinkwell-coordinator  ",
        };

        var built = vm.BuildConnection();

        Assert.Equal(CoordinatorTransport.Remote, built.Transport);
        Assert.Equal("server.lan", built.Machine);
        Assert.Equal("tinkwell-coordinator", built.PipeName);
        Assert.Null(built.DockerContainer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildConnection_for_Docker_carries_container_and_compose_flag(bool useCompose)
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.Docker,
            DockerContainer = "tinkwell",
            UseDockerCompose = useCompose,
        };

        var built = vm.BuildConnection();

        Assert.Equal(CoordinatorTransport.Docker, built.Transport);
        Assert.Equal("tinkwell", built.DockerContainer);
        Assert.Equal(useCompose, built.UseDockerCompose);
        Assert.Null(built.PipeName);
        Assert.Null(built.Machine);
    }

    [Fact]
    public void Visibility_flags_track_the_selected_transport()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe);

        vm.SelectedTransport = CoordinatorTransport.LocalDefault;
        Assert.True(vm.IsLocalDefault);
        Assert.False(vm.IsLocalCustomPipe);
        Assert.False(vm.IsRemote);
        Assert.False(vm.IsDocker);

        vm.SelectedTransport = CoordinatorTransport.Docker;
        Assert.False(vm.IsLocalDefault);
        Assert.True(vm.IsDocker);
    }

    [Theory]
    [InlineData(CoordinatorTransport.LocalDefault)]
    [InlineData(CoordinatorTransport.LocalCustomPipe)]
    [InlineData(CoordinatorTransport.Remote)]
    [InlineData(CoordinatorTransport.Docker)]
    public void Setting_visibility_flag_to_true_switches_transport(CoordinatorTransport transport)
    {
        // The connection dialog binds IsLocalDefault / IsLocalCustomPipe /
        // IsRemote / IsDocker TwoWay to each RadioButton's IsChecked, so the
        // setters must actually flip SelectedTransport for the conditional
        // input rows to appear.
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe);

        switch (transport)
        {
            case CoordinatorTransport.LocalDefault: vm.IsLocalDefault = true; break;
            case CoordinatorTransport.LocalCustomPipe: vm.IsLocalCustomPipe = true; break;
            case CoordinatorTransport.Remote: vm.IsRemote = true; break;
            case CoordinatorTransport.Docker: vm.IsDocker = true; break;
        }

        Assert.Equal(transport, vm.SelectedTransport);
    }

    [Fact]
    public void Setting_visibility_flag_to_false_does_not_change_transport()
    {
        // RadioButton groups fire IsChecked=false on the previously-selected
        // sibling whenever a new one is picked; ignoring those writes is what
        // keeps the new selection from being cleared a moment later.
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.Remote,
        };

        vm.IsLocalDefault = false;

        Assert.Equal(CoordinatorTransport.Remote, vm.SelectedTransport);
    }

    [Fact]
    public void LoadFrom_populates_fields_per_transport()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe);

        vm.LoadFrom(new CoordinatorConnection(
            CoordinatorTransport.Remote, "lab-pipe", "host", null, false));

        Assert.Equal(CoordinatorTransport.Remote, vm.SelectedTransport);
        Assert.Equal("lab-pipe", vm.RemotePipeName);
        Assert.Equal("host", vm.RemoteMachine);
    }

    [Fact]
    public async Task ConnectAsync_raises_Connected_with_chosen_connection_on_success()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.LocalCustomPipe,
            LocalPipeName = "lab-pipe",
        };

        CoordinatorConnection? observed = null;
        vm.Connected += (_, c) => observed = c;

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.NotNull(observed);
        Assert.Equal(CoordinatorTransport.LocalCustomPipe, observed!.Transport);
        Assert.Equal("lab-pipe", observed.PipeName);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);

        Assert.Single(probe.Calls);
        Assert.Equal(CoordinatorTransport.LocalCustomPipe, probe.Calls[0].Transport);
        Assert.Equal("lab-pipe", probe.Calls[0].PipeName);
    }

    [Fact]
    public async Task ConnectAsync_does_not_raise_Connected_on_probe_failure_and_surfaces_error()
    {
        var probe = new FakeProbe(ProbeResult.Failed("coordinator unreachable"));
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.LocalDefault,
        };

        var raised = 0;
        vm.Connected += (_, _) => raised++;

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(0, raised);
        Assert.Contains("coordinator unreachable", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ConnectAsync_rejects_missing_fields_without_calling_probe()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe)
        {
            SelectedTransport = CoordinatorTransport.Docker,
            DockerContainer = "  ",
        };

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(probe.Calls);
    }

    [Fact]
    public void Quit_raises_QuitRequested_and_does_not_call_probe()
    {
        var probe = new FakeProbe(ProbeResult.Ok);
        var vm = new ConnectionDialogViewModel(probe);

        var raised = 0;
        vm.QuitRequested += (_, _) => raised++;

        vm.QuitCommand.Execute(null);

        Assert.Equal(1, raised);
        Assert.Empty(probe.Calls);
    }

    private sealed class FakeProbe : ICoordinatorProbe
    {
        private readonly ProbeResult _result;

        public FakeProbe(ProbeResult result)
        {
            _result = result;
        }

        public List<CoordinatorConnection> Calls { get; } = new();

        public Task<ProbeResult> PingAsync(CoordinatorConnection connection, CancellationToken cancellationToken = default)
        {
            Calls.Add(connection);
            return Task.FromResult(_result);
        }
    }
}
