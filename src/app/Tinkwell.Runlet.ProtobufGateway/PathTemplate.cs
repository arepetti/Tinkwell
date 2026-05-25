namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Parses a match pattern like <c>"/{service}/{method}"</c> or
/// <c>"/rpc/{service}/{method}"</c> at startup and provides:
/// <list type="bullet">
///   <item>A CoAP route pattern for registration (with <c>+</c> wildcards).</item>
///   <item>Extraction of <c>{service}</c> and <c>{method}</c> from a matched path.</item>
/// </list>
/// </summary>
internal sealed class PathTemplate
{
    private readonly string[] _segments;
    private readonly int _serviceIndex;
    private readonly int _methodIndex;

    /// <summary>CoAP route pattern suitable for <c>CoapServer.MapPost</c>.</summary>
    public string RoutePattern { get; }

    public PathTemplate(string matchPattern)
    {
        _segments = matchPattern.Trim('/').Split('/');

        _serviceIndex = -1;
        _methodIndex = -1;

        var routeSegments = new string[_segments.Length];

        for (int i=0; i < _segments.Length; ++i)
        {
            if (_segments[i] == "{service}")
            {
                _serviceIndex = i;
                routeSegments[i] = "+";
            }
            else if (_segments[i] == "{method}")
            {
                _methodIndex = i;
                routeSegments[i] = "+";
            }
            else
            {
                routeSegments[i] = _segments[i];
            }
        }

        if (_serviceIndex < 0 || _methodIndex < 0)
            throw new ArgumentException(
                $"Match pattern '{matchPattern}' must contain both {{service}} and {{method}}.",
                nameof(matchPattern));

        RoutePattern = string.Join("/", routeSegments);
    }

    /// <summary>
    /// Extracts the service and method names from a matched CoAP request path.
    /// Returns <see langword="false"/> if the path does not have enough segments.
    /// </summary>
    public bool TryExtract(string path, out string service, out string method)
    {
        var segments = path.Trim('/').Split('/');

        if (segments.Length != _segments.Length)
        {
            service = "";
            method = "";
            return false;
        }

        service = segments[_serviceIndex];
        method = segments[_methodIndex];
        return !string.IsNullOrEmpty(service) && !string.IsNullOrEmpty(method);
    }
}
