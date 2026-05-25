namespace Tinkwell.Coap;

/// <summary>
/// Matches CoAP URI paths against resource patterns using simple wildcard semantics.
/// </summary>
/// <remarks>
/// <para>
/// The matcher is purely textual and segment-based (segments are the <c>/</c>-separated parts of
/// a path, with empty segments ignored). Two wildcards are supported:
/// </para>
/// <list type="bullet">
///   <item><description><c>+</c> matches exactly one segment at that position.</description></item>
///   <item><description><c>#</c> matches zero or more trailing segments (only meaningful as the final segment of the pattern).</description></item>
/// </list>
/// <para>
/// These conventions match the MQTT-style topic wildcards commonly used by CoAP/LwM2M servers to
/// register resource handlers. Matching is case-sensitive and culture-invariant (ordinal).
/// </para>
/// <example>
/// <code>
/// CoapPathMatcher.IsMatch("/3/0/+",          "/3/0/5");     // true
/// CoapPathMatcher.IsMatch("/3/0/+",          "/3/0/5/6");   // false
/// CoapPathMatcher.IsMatch("/telemetry/#",    "/telemetry"); // true
/// CoapPathMatcher.IsMatch("/telemetry/#",    "/telemetry/room1/temperature"); // true
/// CoapPathMatcher.IsMatch("/sensors/temp",   "/sensors/humidity"); // false
/// </code>
/// </example>
/// </remarks>
public static class CoapPathMatcher
{
    /// <summary>
    /// Tests whether a path matches a pattern.
    /// </summary>
    /// <param name="pattern">The resource pattern (may contain <c>+</c> and <c>#</c> wildcards).</param>
    /// <param name="path">The concrete URI path to test (e.g. taken from <see cref="CoapMessage.UriPath"/>).</param>
    /// <returns><see langword="true"/> if <paramref name="path"/> matches <paramref name="pattern"/>; otherwise <see langword="false"/>.</returns>
    /// <example>
    /// <para>Route a CoAP request to a handler that matches a resource pattern and wildcards.</para>
    /// <code>
    /// if (CoapPathMatcher.IsMatch("/3303/0/+", request.UriPath))
    ///     return await ReadTemperatureInstanceAsync();
    /// if (CoapPathMatcher.IsMatch("/3/0/#", request.UriPath))
    ///     return await LwM2MObjectRequestAsync();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public static bool IsMatch(string pattern, string path)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(path);

        var patternSegments = SplitSegments(pattern);
        var pathSegments = SplitSegments(path);

        for (int i=0; i < patternSegments.Length; ++i)
        {
            if (patternSegments[i] == "#")
                return true;

            if (i >= pathSegments.Length)
                return false;

            if (patternSegments[i] == "+")
                continue;

            if (!string.Equals(patternSegments[i], pathSegments[i], StringComparison.Ordinal))
                return false;
        }

        return patternSegments.Length == pathSegments.Length;
    }

    private static string[] SplitSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
