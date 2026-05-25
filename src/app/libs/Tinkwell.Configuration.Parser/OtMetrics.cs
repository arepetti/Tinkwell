using System.Diagnostics.Metrics;

namespace Tinkwell.Configuration.Parser;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Configuration";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> FilesParsed =
        Meter.CreateCounter<long>(
            "tinkwell.config.files_parsed",
            description: "Number of configuration files parsed");

    public static readonly Counter<long> IncludesResolved =
        Meter.CreateCounter<long>(
            "tinkwell.config.includes_resolved",
            description: "Number of include directives resolved");

    public static readonly Histogram<double> ParseDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.config.parse_duration",
            unit: "ms",
            description: "Duration of configuration file parsing");
}
