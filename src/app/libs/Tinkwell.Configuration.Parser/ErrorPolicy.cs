using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser;

/// <summary>
/// The terminal action to take after all retry attempts have been exhausted.
/// </summary>
public enum ErrorPolicyAction
{
    /// <summary>Log warning, skip this item, continue to the next in the chain.</summary>
    ResumeNext,

    /// <summary>Log error, disable this handler/binding/measure for all future invocations.</summary>
    StopThis,

    /// <summary>Log critical, shut down the host application.</summary>
    StopApplication,

    /// <summary>Publish a failure event to the event bus, then continue.</summary>
    Publish,
}

/// <summary>
/// Retry parameters applied before the terminal <see cref="ErrorPolicyAction"/>.
/// Wait between attempts is <c>DelayMs * BackoffMultiplier^(attempt-1)</c>.
/// </summary>
/// <param name="Count">Maximum number of retry attempts (must be &gt; 0).</param>
/// <param name="DelayMs">Base delay in milliseconds between attempts.</param>
/// <param name="BackoffMultiplier">
/// Multiplier applied per attempt. 1 = fixed delay, 2 = exponential doubling, etc.
/// </param>
public sealed record RetryPolicy(int Count, int DelayMs, double BackoffMultiplier);

/// <summary>
/// Error handling policy parsed from an <c>on error</c> block.
/// Configurable at individual handler/binding/measure level and at
/// parent action/verb block level (child overrides parent).
/// </summary>
/// <param name="Action">The terminal action after retries are exhausted.</param>
/// <param name="Retry">
/// Optional retry configuration. <see langword="null"/> means no retry.
/// </param>
/// <param name="EventName">
/// For <see cref="ErrorPolicyAction.Publish"/>: the event name to publish.
/// </param>
/// <param name="EventProperties">
/// For <see cref="ErrorPolicyAction.Publish"/>: additional properties
/// from the <c>on error publish</c> block body.
/// </param>
public sealed record ErrorPolicy(
    ErrorPolicyAction Action,
    RetryPolicy? Retry,
    string? EventName,
    IReadOnlyDictionary<string, ConfigValue>? EventProperties);
