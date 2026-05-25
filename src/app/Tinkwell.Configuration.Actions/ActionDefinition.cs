using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Configuration.Actions;

/// <summary>
/// An action definition parsed from an <c>action</c> block in a <c>.tw</c>
/// configuration file.
/// </summary>
/// <param name="Name">The unique name of the action.</param>
/// <param name="NameFilter">
/// Optional event name filter from the <c>when</c> modifier.
/// When set, only events whose <c>Name</c> matches are handled.
/// </param>
/// <param name="SourceFilter">
/// Optional event source filter from the <c>source</c> body property.
/// </param>
/// <param name="VerbFilter">
/// Optional event verb filter from the <c>verb</c> body property.
/// </param>
/// <param name="Handlers">
/// The <c>do</c> child blocks, each specifying a handler to execute.
/// </param>
/// <param name="OnError">
/// Optional action-level error policy from an <c>on error</c> child block.
/// Acts as the default for all handlers; handler-level policies override this.
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record ActionDefinition(
    string Name,
    string? NameFilter,
    string? SourceFilter,
    string? VerbFilter,
    IReadOnlyList<ActionHandlerDefinition> Handlers,
    ErrorPolicy? OnError,
    SourceLocation Location);
