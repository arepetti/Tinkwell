using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser.Parsing;

internal abstract record RawTopLevel;

internal sealed record RawSetDirective(
    string Name,
    ConfigValue Value,
    SourceLocation Location) : RawTopLevel;

internal sealed record RawBlock(
    string Type,
    string Name,
    IReadOnlyList<Modifier> Modifiers,
    IReadOnlyList<RawMember> Members,
    SourceLocation Location) : RawTopLevel;

internal abstract record RawMember;

internal sealed record RawProperty(
    string Key,
    ConfigValue Value,
    SourceLocation Location) : RawMember;

internal sealed record RawNestedBlock(RawBlock Block) : RawMember;

internal sealed record RawContentPlaceholder(SourceLocation Location) : RawMember;

internal sealed record RawDocument(IReadOnlyList<RawTopLevel> Items);
