using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Signals.Configuration;

/// <summary>
/// A signal definition parsed from a <c>signal</c> block in a <c>.tw</c>
/// configuration file. Signals can be top-level or nested inside a
/// <c>measure</c> block (inline signals).
/// </summary>
/// <param name="Name">The unique name of the signal.</param>
/// <param name="WhenExpression">
/// The condition expression that triggers the signal. For inline signals,
/// references to <c>value</c> are replaced with the parent measure name.
/// </param>
/// <param name="UntilExpression">
/// Optional hysteresis expression. When present, the signal stays active
/// (suppressing re-fires) until this condition becomes true.
/// </param>
/// <param name="Duration">
/// Optional sustained-condition duration. The <c>when</c> expression must
/// hold continuously for this duration before the signal fires.
/// </param>
/// <param name="ParentMeasure">
/// The name of the enclosing measure block for inline signals;
/// <see langword="null"/> for top-level signals.
/// </param>
/// <param name="Properties">
/// Additional user-defined properties from the signal block body.
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record SignalDefinition(
    string Name,
    string WhenExpression,
    string? UntilExpression,
    SignalDuration? Duration,
    string? ParentMeasure,
    IReadOnlyDictionary<string, string> Properties,
    SourceLocation Location);
