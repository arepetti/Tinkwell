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

    public override Task<WatchdogAssessReply> Assess(WatchdogAssessRequest request, ServerCallContext context)
    {
        WatchdogServiceStatus status = WatchdogServiceStatus.Unknown;
        WatchdogMeasureQuality statusQuality = WatchdogMeasureQuality.Poor;

        var snapshots = _watchdog.GetSnapshots();
        if (snapshots.Length > 0)
        {
            status = (WatchdogServiceStatus)snapshots.Min(x => (int)x.Status);
            statusQuality = (WatchdogMeasureQuality)snapshots.Min(x => (int)x.Quality);
        }

        return Task.FromResult(new WatchdogAssessReply()
        {
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            Status = status,
            StatusQuality = statusQuality,
            Anomaly = _watchdog.IsLatestSampleAnAnomaly ?? false,
            AnomalyQuality = _watchdog.IsLatestSampleAnAnomaly is null ? WatchdogMeasureQuality.Poor : WatchdogMeasureQuality.Acceptable,
        });
    }

    private readonly IWatchdog _watchdog;
}
