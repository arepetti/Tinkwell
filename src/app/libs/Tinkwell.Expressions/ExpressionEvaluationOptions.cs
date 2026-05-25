namespace Tinkwell.Expressions;

/// <summary>
/// Options that control expression evaluation behavior.
/// </summary>
/// <param name="Timeout">
/// <para>
/// Maximum time the <strong>caller</strong> will wait for the result
/// (through <see cref="IExpressionEvaluator"/> methods). If this duration
/// elapses, an <see cref="ExpressionEvaluationException"/> is thrown and
/// the <see cref="System.Threading.CancellationToken"/> for the
/// in-flight <see cref="System.Threading.Tasks.Task"/> is also canceled
/// (except when the timeout is <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>,
/// which disables this behavior).
/// </para>
/// <para>
/// This does <strong>not</strong> guarantee the underlying NCalc
/// work stops immediately: evaluation may still run on a thread-pool
/// thread until it completes. A caller-side timeout only bounds wait time
/// and delivers the exception, not a hard preemption of CPU work.
/// </para>
/// <para>
/// If the caller cancels the operation with the token passed to the
/// evaluation method, an <see cref="T:System.OperationCanceledException"/>
/// (or <see cref="T:System.Threading.Tasks.TaskCanceledException"/>) is
/// thrown instead, which is <strong>not</strong> wrapped in
/// <see cref="ExpressionEvaluationException"/>.
/// </para>
/// <para>
/// Default when omitted is 5 seconds. Use
/// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable
/// the timer-based bound (subject only to caller cancellation and host policy).
/// </para>
/// </param>
public sealed record ExpressionEvaluationOptions(TimeSpan? Timeout = null)
{
    /// <summary>
    /// The default timeout applied when no explicit value is provided.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The effective timeout, falling back to <see cref="DefaultTimeout"/>
    /// when not explicitly set.
    /// </summary>
    public TimeSpan EffectiveTimeout => Timeout ?? DefaultTimeout;

    /// <summary>
    /// Default options: 5-second timeout.
    /// </summary>
    public static ExpressionEvaluationOptions Default { get; } = new();
}
