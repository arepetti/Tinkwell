using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Services;

namespace Tinkwell.Watchdog.Services;

sealed class WatchdogService : Tinkwell.Services.Watchdog.WatchdogBase
{
    public WatchdogService(IWatchdog watchdog)
    {
        _watchdog = watchdog;
    }

    public override Task<WatchdogListReply> List(WatchdogListRequest request, ServerCallContext context)
    {
        var snapshots = _watchdog.GetSnapshots();
        if (request.HasQuery && !string.IsNullOrWhiteSpace(request.Query))
        {
            var match = TextHelpers.PatternToRegex(request.Query);
            snapshots = snapshots.Where(x => match.IsMatch(x.Runner.Name)).ToArray();
        }

        var response = new WatchdogListReply();
        response.Runners.AddRange(snapshots.Select(snapshot =>
        {
            return new RunnerHealthStatus
            {
                Name = snapshot.Runner.Name,
                Timestamp = Timestamp.FromDateTime(snapshot.Timestamp),
                Quality = snapshot.Quality switch
                {
                    SnapshotQuality.Good => WatchdogMeasureQuality.Good,
                    SnapshotQuality.Acceptable => WatchdogMeasureQuality.Acceptable,
                    _ => WatchdogMeasureQuality.Poor,
                },
                Status = snapshot.Status switch
                {
                    ServiceStatus.Undefined => WatchdogServiceStatus.Undefined,
                    ServiceStatus.Serving => WatchdogServiceStatus.Serving,
                    ServiceStatus.Degraded => WatchdogServiceStatus.Degraded,
                    ServiceStatus.Crashed => WatchdogServiceStatus.Crashed,
                    _ => WatchdogServiceStatus.Unknown
                },
                Resources = new()
                {
                    CpuUtilization = (float)snapshot.CpuUsage,
                    MemoryUsage = (float)snapshot.Memory,
                    PeakMemoryUsage = (float)snapshot.PeakMemory,
                    HandleCount = (uint)snapshot.HandleCount,
                    ThreadCount = (uint)snapshot.ThredCount
                }
            };
        }));

        return Task.FromResult(response);
    }

    private readonly IWatchdog _watchdog;
}
