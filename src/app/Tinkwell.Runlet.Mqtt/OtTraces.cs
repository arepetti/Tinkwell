using System.Diagnostics;

namespace Tinkwell.Runlet.Mqtt;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Mqtt";
    public static readonly ActivitySource Source = new(SourceName);

    public const string Connect = "mqtt.connect";

    public const string ConnectionName = "mqtt.connection";
    public const string ConnectResult = "connect.result";
}
