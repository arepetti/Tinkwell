using System.Diagnostics.Metrics;

namespace Tinkwell.Runlet.Coap;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Coap";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("tinkwell.coap.requests",
            description: "Total CoAP requests received");

    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("tinkwell.coap.request_duration",
            unit: "ms", description: "CoAP request processing duration");
}
