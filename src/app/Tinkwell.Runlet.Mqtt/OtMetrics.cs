using System.Diagnostics.Metrics;

namespace Tinkwell.Runlet.Mqtt;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Mqtt";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ConnectAttempts =
        Meter.CreateCounter<long>(
            "tinkwell.mqtt.connect_attempts",
            description: "MQTT broker connection attempts");

    public static readonly Histogram<double> ConnectDuration =
        Meter.CreateHistogram<double>(
            "tinkwell.mqtt.connect_duration",
            unit: "ms",
            description: "Time to establish an MQTT broker connection");
}
