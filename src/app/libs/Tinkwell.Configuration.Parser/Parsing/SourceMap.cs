namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Maps line numbers in merged (post-include) text back to original file locations.
/// </summary>
internal sealed class SourceMap
{
    private readonly List<SourceSpan> _spans = [];

    public void AddSpan(int mergedStartLine, int lineCount, string filePath, int originalStartLine)
    {
        _spans.Add(new SourceSpan(mergedStartLine, lineCount, filePath, originalStartLine));
    }

    public SourceLocation Resolve(int mergedLine, int column)
    {
        // Spans are added in order of non-decreasing MergedStartLine; find the
        // rightmost span whose start line is <= mergedLine, then test membership.
        int lo = 0, hi = _spans.Count - 1, candidate = -1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            int midStart = _spans[mid].MergedStartLine;
            if (midStart <= mergedLine)
            {
                candidate = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (candidate >= 0)
        {
            var span = _spans[candidate];
            if (mergedLine < span.MergedStartLine + span.LineCount)
            {
                int originalLine = span.OriginalStartLine + (mergedLine - span.MergedStartLine);
                return new SourceLocation(span.FilePath, originalLine, column);
            }
        }

        return new SourceLocation("<unknown>", mergedLine, column);
    }

    private sealed record SourceSpan(int MergedStartLine, int LineCount, string FilePath, int OriginalStartLine);
}
