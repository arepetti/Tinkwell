using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Configuration.Actions;

/// <summary>
/// A handler invocation parsed from a <c>do</c> child block inside an
/// <c>action</c> block.
/// </summary>
/// <param name="HandlerName">
/// The handler name (e.g. <c>log</c>, <c>create-event</c>, <c>update-measure</c>).
/// </param>
/// <param name="AssemblyPath">
/// The assembly path from the <c>from</c> modifier, or <see langword="null"/>
/// for built-in handlers.
/// </param>
/// <param name="Parameters">
/// Raw <see cref="ConfigValue"/> parameters from the <c>do</c> block body.
/// <see cref="ExpressionValue"/> instances are preserved for runtime evaluation;
/// <see cref="StringValue"/> instances (including resolved <c>$"..."</c> templates)
/// are static.
/// </param>
/// <param name="OnError">
/// Optional handler-level error policy from an <c>on error</c> child block.
/// Overrides the parent <see cref="ActionDefinition.OnError"/> when set.
/// </param>
/// <param name="Location">Source location for diagnostics.</param>
public sealed record ActionHandlerDefinition(
    string HandlerName,
    string? AssemblyPath,
    IReadOnlyDictionary<string, ConfigValue> Parameters,
    ErrorPolicy? OnError,
    SourceLocation Location);
