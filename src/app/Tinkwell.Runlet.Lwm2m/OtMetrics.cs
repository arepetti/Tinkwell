using System.Diagnostics.Metrics;

namespace Tinkwell.Runlet.Lwm2m;

internal static class OtMetrics
{
    public const string MeterName = "Tinkwell.Lwm2m";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("tinkwell.lwm2m.requests",
            description: "Total LwM2M CoAP requests received");

    public static readonly Counter<long> Registrations =
        Meter.CreateCounter<long>("tinkwell.lwm2m.registrations",
            description: "Total client registrations (register + update)");

    public static readonly Counter<long> Writes =
        Meter.CreateCounter<long>("tinkwell.lwm2m.writes",
            description: "Total successful resource writes");

    public static readonly UpDownCounter<long> ActiveClients =
        Meter.CreateUpDownCounter<long>("tinkwell.lwm2m.active_clients",
            description: "Currently registered clients");
}
