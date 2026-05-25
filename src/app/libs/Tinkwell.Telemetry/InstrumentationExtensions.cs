using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Tinkwell.Telemetry;

/// <summary>
/// Convenience extension methods that reduce telemetry boilerplate at call sites.
/// </summary>
/// <example>
/// Define your trace/metric constants once, then use the extensions at call sites:
/// <code>
/// static class OtTraces
/// {
///     public static readonly ActivitySource Source = new("MyService.Traces");
///     public const string RunnerLaunch = "runner.launch";
///     public const string RunnerName   = "runner.name";
///     public const string RunnerId     = "runner.id";
/// }
///
/// static class OtMetrics
/// {
///     private static readonly Meter Meter = new("MyService.Metrics");
///     public static readonly Counter&lt;long&gt; RunnersLaunched =
///         Meter.CreateCounter&lt;long&gt;("runners.launched");
///     public static readonly Histogram&lt;double&gt; DiscoveryDuration =
///         Meter.CreateHistogram&lt;double&gt;("discovery.duration_ms");
/// }
/// </code>
/// </example>
public static class InstrumentationExtensions
{
    /// <summary>
    /// Starts an <see cref="Activity"/> and applies zero or more tags in one call.
    /// </summary>
    /// <example>
    /// Start a span for a runner launch with two tags:
    /// <code>
    /// using var activity = OtTraces.Source.Start(
    ///     OtTraces.RunnerLaunch,
    ///     (OtTraces.RunnerName, runner.Config.Name),
    ///     (OtTraces.RunnerId, runner.Id));
    ///
    /// await LaunchProcessAsync(runner, ct);
    /// </code>
    /// Start a span with no tags:
    /// <code>
    /// using var activity = OtTraces.Source.Start("ValidateConfig");
    /// </code>
    /// </example>
    public static Activity? Start(
        this ActivitySource source,
        string name,
        params ReadOnlySpan<(string Key, object? Value)> tags)
    {
        var activity = source.StartActivity(name);
        if (activity is not null)
        {
            foreach (var (key, value) in tags)
                activity.SetTag(key, value);
        }
        return activity;
    }

    /// <summary>
    /// Shorthand for <see cref="Activity.SetStatus"/> with <see cref="ActivityStatusCode.Error"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var activity = OtTraces.Source.Start("pipe.send", ("command", cmd));
    /// try
    /// {
    ///     await pipe.SendAsync(cmd, ct);
    /// }
    /// catch (TimeoutException ex)
    /// {
    ///     activity.Error($"Pipe timed out: {ex.Message}");
    ///     throw;
    /// }
    /// </code>
    /// </example>
    public static void Error(this Activity? activity, string message) =>
        activity?.SetStatus(ActivityStatusCode.Error, message);

    /// <summary>
    /// Increments a counter by 1 with a single tag.
    /// </summary>
    /// <example>
    /// Count launched runners by name:
    /// <code>
    /// OtMetrics.RunnersLaunched.Inc(OtTraces.RunnerName, runner.Config.Name);
    /// </code>
    /// Count completed jobs by outcome:
    /// <code>
    /// OtMetrics.JobsCompleted.Inc("outcome", "success");
    /// </code>
    /// </example>
    public static void Inc(this Counter<long> counter, string tagKey, object? tagValue) =>
        counter.Add(1, new KeyValuePair<string, object?>(tagKey, tagValue));

    /// <summary>
    /// Records a histogram value with a single tag.
    /// </summary>
    /// <example>
    /// Record a discovery duration tagged by service name:
    /// <code>
    /// var elapsed = stopwatch.Elapsed.TotalMilliseconds;
    /// OtMetrics.DiscoveryDuration.Record(elapsed, "service.name", serviceName);
    /// </code>
    /// </example>
    public static void Record(this Histogram<double> histogram, double value, string tagKey, object? tagValue) =>
        histogram.Record(value, new KeyValuePair<string, object?>(tagKey, tagValue));
}
