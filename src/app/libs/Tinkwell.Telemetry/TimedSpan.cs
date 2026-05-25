using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Tinkwell.Telemetry;

/// <summary>
/// Combines an <see cref="Activity"/> span with a <see cref="Stopwatch"/>
/// timestamp. On dispose, the elapsed milliseconds are recorded into an
/// optional <see cref="Histogram{T}"/> and the activity is disposed.
/// </summary>
/// <example>
/// Wrap an async operation so it is both traced and measured:
/// <code>
/// using var span = OtTraces.Source.Timed(
///     "ParseConfig", OtMetrics.ParseDuration,
///     ("config.path", filePath));
///
/// var result = await parser.ParseAsync(filePath, ct);
///
/// if (result.HasErrors)
///     span.Error("Parse failed");
/// // duration is recorded automatically when span is disposed
/// </code>
/// </example>
public readonly struct TimedSpan : IDisposable
{
    private readonly Activity? _activity;
    private readonly long _startTimestamp;
    private readonly Histogram<double>? _histogram;

    internal TimedSpan(Activity? activity, Histogram<double>? histogram)
    {
        _activity = activity;
        _startTimestamp = Stopwatch.GetTimestamp();
        _histogram = histogram;
    }

    /// <summary>The underlying activity, if one was started.</summary>
    public Activity? Activity => _activity;

    /// <summary>Sets a tag on the underlying activity span.</summary>
    /// <example>
    /// <code>
    /// using var span = source.Timed("ProcessBatch", histogram);
    /// span.SetTag("batch.size", items.Count);
    /// span.SetTag("batch.source", "mqtt");
    /// </code>
    /// </example>
    public void SetTag(string key, object? value) => _activity?.SetTag(key, value);

    /// <summary>Marks the span as failed with the given error message.</summary>
    /// <example>
    /// <code>
    /// using var span = source.Timed("Connect", connectDuration);
    /// try
    /// {
    ///     await client.ConnectAsync(ct);
    /// }
    /// catch (Exception ex)
    /// {
    ///     span.Error(ex.Message);
    ///     throw;
    /// }
    /// </code>
    /// </example>
    public void Error(string message) => _activity?.SetStatus(ActivityStatusCode.Error, message);

    /// <summary>Records elapsed time to the histogram and disposes the activity.</summary>
    public void Dispose()
    {
        if (_histogram is not null)
            _histogram.Record(Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds);
        _activity?.Dispose();
    }
}

/// <summary>
/// Extension to create a <see cref="TimedSpan"/> from an <see cref="ActivitySource"/>.
/// </summary>
/// <example>
/// Typical usage inside a service class that defines its own trace constants:
/// <code>
/// static class OtTraces
/// {
///     public static readonly ActivitySource Source = new("MyRunner.Traces");
///     public const string Connect = "mqtt.connect";
/// }
///
/// static class OtMetrics
/// {
///     private static readonly Meter Meter = new("MyRunner.Metrics");
///     public static readonly Histogram&lt;double&gt; ConnectDuration =
///         Meter.CreateHistogram&lt;double&gt;("mqtt.connect.duration_ms");
/// }
///
/// // In your method:
/// using var span = OtTraces.Source.Timed(
///     OtTraces.Connect, OtMetrics.ConnectDuration,
///     ("connection.name", connectionName));
/// </code>
/// </example>
public static class TimedSpanExtensions
{
    /// <summary>
    /// Starts a span that automatically records its duration to <paramref name="histogram"/>
    /// when disposed. Tags can be set on the returned <see cref="TimedSpan"/>.
    /// </summary>
    /// <example>
    /// Trace-only (no histogram):
    /// <code>
    /// using var span = OtTraces.Source.Timed("CacheCheck");
    /// </code>
    /// With a histogram and initial tags:
    /// <code>
    /// using var span = OtTraces.Source.Timed(
    ///     "HostBuild", OtMetrics.HostBuildDuration,
    ///     ("runner.name", name), ("runner.id", id));
    /// </code>
    /// </example>
    public static TimedSpan Timed(
        this ActivitySource source,
        string name,
        Histogram<double>? histogram = null,
        params ReadOnlySpan<(string Key, object? Value)> tags)
    {
        var activity = source.StartActivity(name);
        if (activity is not null)
        {
            foreach (var (key, value) in tags)
                activity.SetTag(key, value);
        }
        return new TimedSpan(activity, histogram);
    }
}
