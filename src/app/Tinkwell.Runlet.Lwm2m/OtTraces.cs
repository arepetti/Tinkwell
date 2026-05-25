using System.Diagnostics;

namespace Tinkwell.Runlet.Lwm2m;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Lwm2m";

    public static readonly ActivitySource Source = new(SourceName);

    public const string ServerName = "lwm2m.server";
    public const string Method = "lwm2m.method";
    public const string Path = "lwm2m.path";
    public const string ResponseCode = "lwm2m.response_code";
    public const string ClientEndpoint = "lwm2m.client_endpoint";
}
