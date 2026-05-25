using System.Diagnostics;

namespace Tinkwell.Runlet.Coap;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Coap";

    public static readonly ActivitySource Source = new(SourceName);

    public const string ServerName = "coap.server";
    public const string Method = "coap.method";
    public const string Path = "coap.path";
    public const string ResponseCode = "coap.response_code";
}
