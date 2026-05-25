using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Studio.Services;
using Xunit;

namespace Tinkwell.Studio.Tests;

public class CoordinatorHeartbeatTests
{
    [Fact]
    public async Task PingNow_reports_online_when_stub_succeeds()
    {
        var stub = StubCli.Create(new[] { "{\"pong\":true}" });
        var settings = new StudioSettings
        {
            TwExecutablePath = stub,
            HeartbeatInterval = TimeSpan.FromMinutes(1),
        };
        var cli = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);
        using var heartbeat = new CoordinatorHeartbeat(
            cli, settings, NullLogger<CoordinatorHeartbeat>.Instance);

        await heartbeat.PingNowAsync();

        Assert.Equal(CoordinatorConnectivity.Online, heartbeat.Current.Connectivity);
        Assert.NotNull(heartbeat.Current.Latency);
    }

    [Fact]
    public async Task PingNow_reports_offline_when_stub_fails()
    {
        var stub = StubCli.Create(
            stdoutLines: Array.Empty<string>(),
            exitCode: 1,
            stderr: "coordinator unreachable");
        var settings = new StudioSettings
        {
            TwExecutablePath = stub,
            HeartbeatInterval = TimeSpan.FromMinutes(1),
        };
        var cli = new TwCliProcessRunner(settings, NullLogger<TwCliProcessRunner>.Instance);
        using var heartbeat = new CoordinatorHeartbeat(
            cli, settings, NullLogger<CoordinatorHeartbeat>.Instance);

        CoordinatorStatus? observed = null;
        heartbeat.Changed += (_, s) => observed = s;

        await heartbeat.PingNowAsync();

        Assert.Equal(CoordinatorConnectivity.Offline, heartbeat.Current.Connectivity);
        Assert.NotNull(heartbeat.Current.LastError);
        Assert.NotNull(observed);
    }
}
