using System.Diagnostics.Metrics;

namespace Tinkwell.Runner.Hosting;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Runner";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RunletsLoaded =
        Meter.CreateCounter<long>(
            "tinkwell.runner.runlets_loaded",
            description: "Number of runlets loaded");

    public static readonly Histogram<double> HostBuildDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.runner.host_build_duration",
            unit: "ms",
            description: "Duration of host building in a runner");

    public static readonly Histogram<double> LifecycleDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.runner.startup_duration",
            unit: "ms",
            description: "Total runner startup time (parse to ready)");

    // --- Service discovery ---

    public static readonly Counter<long> DiscoveryCalls =
        Meter.CreateCounter<long>(
            "tinkwell.runner.discovery_calls",
            description: "Service discovery attempts");

    public static readonly Histogram<double> DiscoveryDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.runner.discovery_duration",
            unit: "ms",
            description: "Time to discover a service via the coordinator pipe");

    // --- gRPC channel pool ---

    public static readonly Counter<long> ChannelCacheHits =
        Meter.CreateCounter<long>(
            "tinkwell.runner.channel_cache_hits",
            description: "Reused an existing gRPC channel from the pool");

    public static readonly Counter<long> ChannelCacheMisses =
        Meter.CreateCounter<long>(
            "tinkwell.runner.channel_cache_misses",
            description: "Created a new gRPC channel (cache miss)");

    public static readonly Histogram<double> ChannelCreateDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.runner.channel_create_duration",
            unit: "ms",
            description: "Time to create a gRPC channel (fresh or pooled)");
}
