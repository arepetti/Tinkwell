namespace Tinkwell.Events;

/// <summary>
/// Well-known verbs for the event bus. Use <see cref="Custom"/> with
/// <see cref="EventEnvelope.CustomVerb"/> for domain-specific verbs
/// not covered here.
/// </summary>
public enum EventVerb
{
    /// <summary>Domain-specific verb; use with <see cref="EventEnvelope.CustomVerb"/>.</summary>
    Custom = 0,
    /// <summary>Entity fired (e.g. signal event).</summary>
    Fired,
    /// <summary>Entity or value changed.</summary>
    Changed,
    /// <summary>Entity was created.</summary>
    Created,
    /// <summary>Entity was removed.</summary>
    Deleted,
    /// <summary>Time-bound entity expired.</summary>
    Expired,
    /// <summary>Process or operation started.</summary>
    Started,
    /// <summary>Process or operation stopped.</summary>
    Stopped,
    /// <summary>Operation or delivery failed.</summary>
    Failed,
}
