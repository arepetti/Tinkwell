using Tinkwell.Configuration.Parser.Parsing;

namespace Tinkwell.Configuration.Parser.Tests;

/// <summary>
/// <see cref="SourceMap.Resolve"/> uses binary search (PERF-1): behavior must stay correct for many spans.
/// </summary>
public class SourceMapTests
{
    [Fact]
    public void Resolve_FirstMiddleAndLastSpans_MapToOriginalLines()
    {
        var map = new SourceMap();
        map.AddSpan(mergedStartLine: 1, lineCount: 2, filePath: "a.tw", originalStartLine: 10);
        map.AddSpan(mergedStartLine: 3, lineCount: 3, filePath: "b.tw", originalStartLine: 1);
        map.AddSpan(mergedStartLine: 6, lineCount: 1, filePath: "c.tw", originalStartLine: 5);

        Assert.Equal(new SourceLocation("a.tw", 10, 4), map.Resolve(1, 4));
        Assert.Equal(new SourceLocation("a.tw", 11, 1), map.Resolve(2, 1));

        Assert.Equal(new SourceLocation("b.tw", 1, 2), map.Resolve(3, 2));
        Assert.Equal(new SourceLocation("b.tw", 3, 1), map.Resolve(5, 1));

        Assert.Equal(new SourceLocation("c.tw", 5, 9), map.Resolve(6, 9));
    }

    [Fact]
    public void Resolve_LineJustPastSpanEnd_FallsBackToUnknown()
    {
        var map = new SourceMap();
        map.AddSpan(1, 2, "f.tw", 1);
        var loc = map.Resolve(3, 1);
        Assert.Equal("<unknown>", loc.FilePath);
        Assert.Equal(3, loc.Line);
    }

    [Fact]
    public void Resolve_EmptyMap_Unknown()
    {
        var map = new SourceMap();
        var loc = map.Resolve(1, 1);
        Assert.Equal("<unknown>", loc.FilePath);
    }
}
