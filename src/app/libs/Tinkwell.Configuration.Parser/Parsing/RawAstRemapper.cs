namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Rewrites every <see cref="SourceLocation"/> in a <see cref="RawDocument"/> so
/// that its <see cref="SourceLocation.FilePath"/> and <see cref="SourceLocation.Line"/>
/// refer to the original source file (rather than a line in the merged text produced
/// by <see cref="IncludeResolver"/>).
/// </summary>
internal static class RawAstRemapper
{
    public static RawDocument Remap(RawDocument document, SourceMap sourceMap)
    {
        var items = new List<RawTopLevel>(document.Items.Count);
        foreach (var item in document.Items)
        {
            items.Add(item switch
            {
                RawSetDirective set => RemapSet(set, sourceMap),
                RawBlock block => RemapBlock(block, sourceMap),
                _ => item
            });
        }
        return new RawDocument(items);
    }

    private static RawSetDirective RemapSet(RawSetDirective set, SourceMap sourceMap) =>
        set with { Location = Resolve(set.Location, sourceMap) };

    private static RawBlock RemapBlock(RawBlock block, SourceMap sourceMap)
    {
        var members = new List<RawMember>(block.Members.Count);
        foreach (var member in block.Members)
        {
            members.Add(member switch
            {
                RawProperty prop => prop with { Location = Resolve(prop.Location, sourceMap) },
                RawNestedBlock nested => new RawNestedBlock(RemapBlock(nested.Block, sourceMap)),
                RawContentPlaceholder placeholder =>
                    placeholder with { Location = Resolve(placeholder.Location, sourceMap) },
                _ => member
            });
        }

        return block with
        {
            Location = Resolve(block.Location, sourceMap),
            Members = members,
        };
    }

    private static SourceLocation Resolve(SourceLocation location, SourceMap sourceMap)
    {
        // The grammar emits placeholder `new SourceLocation("", line, col)` nodes
        // where `line` is relative to the merged (post-include) text. We translate
        // them here once so downstream code only ever sees real file-relative
        // locations. Locations produced with a non-zero line but an already-filled
        // FilePath (e.g. future synthesised locations) are left untouched.
        if (!string.IsNullOrEmpty(location.FilePath))
            return location;

        if (location.Line <= 0)
            return location;

        return sourceMap.Resolve(location.Line, location.Column);
    }
}
