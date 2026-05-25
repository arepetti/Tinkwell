namespace Tinkwell.Studio.Services;

/// <summary>
/// Visual tone for the round per-row "avatar" indicator used across the
/// Studio category lists. The view layer (a tiny <c>Indicator</c> user
/// control) maps each value to a pair of theme brushes — background fill
/// and glyph foreground — so the same vocabulary describes the chip
/// regardless of the host theme.
/// </summary>
/// <remarks>
/// Kept in <c>Core</c> rather than the WinUI project so view models can
/// classify rows (a <c>RunnerRow</c> picks <see cref="Success"/> /
/// <see cref="Warning"/> / <see cref="Critical"/> from its status, a
/// <c>MeasureEntry</c> picks <see cref="Accent"/> for derived measures, ...)
/// without dragging in WinUI types.
/// </remarks>
public enum IndicatorTone
{
    /// <summary>Light gray. Neutral / unclassified.</summary>
    Default,

    /// <summary>Darker gray. "Disabled" / "frozen" things (e.g. a constant
    /// measure that never changes, or a system-managed entity).</summary>
    Muted,

    /// <summary>Theme accent (blue in stock Fluent). Highlights an entity
    /// that is special but not stateful, e.g. a derived/calculated measure.</summary>
    Accent,

    /// <summary>Green. The entity is healthy / running / OK.</summary>
    Success,

    /// <summary>Amber. The entity is in a transitional state (starting,
    /// restarting, degraded, ...).</summary>
    Warning,

    /// <summary>Red. The entity is stopped, crashed, or unhealthy.</summary>
    Critical,
}
