namespace Tinkwell.Events;

/// <summary>
/// Canonical event model following a Subject-Verb-Object pattern.
/// <list type="bullet">
///   <item><see cref="Source"/> + <see cref="Name"/> form the subject (who/what).</item>
///   <item><see cref="Verb"/> describes what happened.</item>
///   <item><see cref="Object"/> is an optional value or target.</item>
/// </list>
/// </summary>
public sealed record EventEnvelope
{
    /// <summary>The producing subsystem (plain string, e.g. "signals", "measures").</summary>
    public required string Source { get; init; }

    /// <summary>What happened.</summary>
    public required EventVerb Verb { get; init; }

    /// <summary>Free-form verb string when <see cref="Verb"/> is <see cref="EventVerb.Custom"/>.</summary>
    public string? CustomVerb { get; init; }

    /// <summary>The entity name (signal name, measure name, etc.).</summary>
    public required string Name { get; init; }

    /// <summary>Optional value or target string.</summary>
    public string? Object { get; init; }

    /// <summary>
    /// Optional id for correlating work across subsystems. Not populated unless
    /// the publisher or tooling sets it (e.g. <c>tw events publish</c> may assign one).
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>When the event was created (UTC).</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Arbitrary extra properties.</summary>
    public IReadOnlyDictionary<string, string> Payload { get; init; } =
        new Dictionary<string, string>();
}
