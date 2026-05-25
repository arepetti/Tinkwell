namespace Tinkwell.Runlet.Signals.Configuration;

/// <summary>
/// Represents the <c>for</c> duration clause of a signal. A duration can be
/// a fixed number of seconds, a parseable duration string, or a dynamic
/// expression that evaluates to seconds at runtime.
/// </summary>
public abstract record SignalDuration
{
    private SignalDuration() { }

    /// <summary>
    /// A fixed duration in seconds, from a numeric literal (e.g. <c>for 10</c>).
    /// </summary>
    public sealed record Seconds(double Value) : SignalDuration;

    /// <summary>
    /// A duration string parseable by UnitsNet (e.g. <c>for "10 seconds"</c>).
    /// Resolved to seconds at startup.
    /// </summary>
    public sealed record Parsed(string Text) : SignalDuration;

    /// <summary>
    /// A dynamic expression that evaluates to seconds at runtime
    /// (e.g. <c>for (cycle_duration / 10)</c>).
    /// </summary>
    public sealed record Expression(string Text) : SignalDuration;
}
