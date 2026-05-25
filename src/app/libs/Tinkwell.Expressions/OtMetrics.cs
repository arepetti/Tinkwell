using System.Diagnostics.Metrics;

namespace Tinkwell.Expressions;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Expressions";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Evaluations =
        Meter.CreateCounter<long>(
            "tinkwell.expressions.evaluations",
            description: "Number of expression evaluations");

    // Counts scheduler-side timeouts: the caller’s wait was stopped by the timer, not
    // a forced halt of the NCalc work (which may still run in the background).
    public static readonly Counter<long> Timeouts =
        Meter.CreateCounter<long>(
            "tinkwell.expressions.timeouts",
            description: "Count of call waits ended by the configured evaluation timeout (thread-pool work may still complete afterward)");

    public static readonly Histogram<double> EvaluationDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.expressions.duration",
            unit: "ms",
            description: "Duration of expression evaluation");

    public static readonly Counter<long> ParseCacheHits =
        Meter.CreateCounter<long>(
            "tinkwell.expressions.parse_cache.hits",
            description: "Number of expression parses served from the in-memory AST cache");

    public static readonly Counter<long> ParseCacheMisses =
        Meter.CreateCounter<long>(
            "tinkwell.expressions.parse_cache.misses",
            description: "Number of expression parses that ran the NCalc parser");

    public static readonly Counter<long> ParseCacheEvictions =
        Meter.CreateCounter<long>(
            "tinkwell.expressions.parse_cache.evictions",
            description: "Number of cached expressions removed by the LRU policy");
}
