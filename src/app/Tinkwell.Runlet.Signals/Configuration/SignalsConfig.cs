namespace Tinkwell.Runlet.Signals.Configuration;

/// <summary>
/// Root configuration produced by parsing signal definitions from a
/// <c>.tw</c> configuration file. Contains both top-level signals and
/// signals extracted from inline <c>signal</c> blocks inside measures.
/// </summary>
/// <param name="Signals">The signal definitions in source order.</param>
public sealed record SignalsConfig(IReadOnlyList<SignalDefinition> Signals);
