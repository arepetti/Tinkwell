using System.Diagnostics.Metrics;

namespace Tinkwell.Coordinator;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Coordinator";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RunnersLaunched =
        Meter.CreateCounter<long>(
            "tinkwell.coordinator.runners_launched",
            description: "Number of runner processes launched");

    public static readonly Counter<long> RunnersCrashed =
        Meter.CreateCounter<long>(
            "tinkwell.coordinator.runners_crashed",
            description: "Number of runner process crashes");

    public static readonly Counter<long> RunnersRestarted =
        Meter.CreateCounter<long>(
            "tinkwell.coordinator.runners_restarted",
            description: "Number of runner process restarts");

    public static readonly Histogram<double> RunnerStartupDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.coordinator.runner_startup_duration",
            unit: "ms",
            description: "Time from runner launch to ready signal");

    public static readonly Counter<long> CommandsDispatched =
        Meter.CreateCounter<long>(
            "tinkwell.coordinator.commands",
            description: "Number of pipe commands dispatched");

    public static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.coordinator.command_duration",
            unit: "ms",
            description: "Duration of pipe command processing");
}
