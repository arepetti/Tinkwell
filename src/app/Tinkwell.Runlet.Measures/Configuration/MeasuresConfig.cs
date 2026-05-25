using Tinkwell.Measures;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Measures.Configuration;

/// <summary>
/// Root configuration produced by parsing a <c>.tw</c> measures file.
/// Contains the ordered list of measure definitions that a runlet will
/// register with the measure registry.
/// </summary>
/// <param name="Measures">The measure entries, in the order they appear in the configuration file.</param>
public sealed record MeasuresConfig(IReadOnlyList<MeasureConfigEntry> Measures);

/// <summary>
/// A single measure parsed from a <c>measure</c> block. Combines the
/// definition, metadata, and an optional value string.
/// </summary>
/// <param name="Definition">The measure schema (type, unit, range, etc.).</param>
/// <param name="Metadata">Optional description, category, and tags.</param>
/// <param name="Value">
/// <see langword="null"/> for a plain measure (updated externally).
/// A numeric string for a constant. An expression string for a derived measure.
/// </param>
/// <param name="OnError">
/// Optional error policy from an <c>on error</c> child block.
/// Only meaningful for derived measures (those with an expression <see cref="Value"/>).
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record MeasureConfigEntry(
    MeasureDefinition Definition,
    MeasureMetadata Metadata,
    string? Value,
    ErrorPolicy? OnError,
    SourceLocation Location);
