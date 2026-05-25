using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Tinkwell.Telemetry;


namespace Tinkwell.Configuration.Parser.Parsing;

/// <summary>
/// Resolves include directives by inlining included file content.
/// Builds a source map for accurate error reporting across files.
/// </summary>
internal sealed partial class IncludeResolver
{
    private readonly IFileProvider _fileProvider;
    private readonly ILogger? _logger;
    private readonly HashSet<string> _visited = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ConfigurationDiagnostic> _warnings = [];
    private int _currentMergedLine = 1;

    public IncludeResolver(IFileProvider fileProvider, ILogger? logger)
    {
        _fileProvider = fileProvider;
        _logger = logger;
    }

    public async Task<(string MergedText, SourceMap SourceMap, IReadOnlyList<ConfigurationDiagnostic> Warnings)> ResolveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var sourceMap = new SourceMap();

        await ResolveRecursiveAsync(
            path, referencedFromFile: null, referencedFromLine: 0,
            sb, sourceMap, includeParent: null, cancellationToken);

        return (sb.ToString(), sourceMap, _warnings);
    }

    private async Task ResolveRecursiveAsync(
        string path,
        string? referencedFromFile,
        int referencedFromLine,
        StringBuilder sb,
        SourceMap sourceMap,
        IncludeFrame? includeParent,
        CancellationToken cancellationToken)
    {
        var normalizedPath = path.Replace('\\', '/');

        if (!_visited.Add(normalizedPath))
        {
            var (warnFile, warnLine) = DirectiveLocation(
                referencedFromFile, referencedFromLine, normalizedPath);
            // Recorded as a non-fatal diagnostic; ConfigurationParser logs the
            // aggregated list at Warning level once parsing completes.
            _warnings.Add(new ConfigurationDiagnostic(
                $"Duplicate include of '{normalizedPath}' ignored; the file was already included.",
                warnFile, warnLine, 1));
            _logger?.LogTrace(
                "Duplicate include of '{Path}' ignored at {File}:{Line}",
                normalizedPath, warnFile, warnLine);
            return;
        }

        var currentFrame = new IncludeFrame(includeParent, normalizedPath);
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Include chain: {Chain}", FormatIncludeChain(currentFrame));

        var fileInfo = _fileProvider.GetFileInfo(normalizedPath);
        if (!fileInfo.Exists)
        {
            _logger?.LogError("Include file '{Path}' not found. Provider: {Provider}",
                normalizedPath, _fileProvider.GetType().Name);
            var (missingFile, missingLine) = DirectiveLocation(
                referencedFromFile, referencedFromLine, normalizedPath);
            throw new ConfigurationFileNotFoundException(
                normalizedPath, missingFile, missingLine);
        }

        _logger?.LogTrace("Including '{Path}' (resolved: {PhysicalPath})",
            normalizedPath, fileInfo.PhysicalPath ?? normalizedPath);

        string content;
        using (var stream = fileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        var lines = content.Split('\n');
        bool inDirectiveSection = true;

        var bodyLines = new List<(string line, int originalLineNumber)>();

        for (int i=0; i < lines.Length; ++i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trimmed = lines[i].TrimStart();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith("//"))
            {
                bodyLines.Add((lines[i], i + 1));
                continue;
            }

            if (inDirectiveSection && trimmed.StartsWith("include "))
            {
                var match = IncludePattern().Match(trimmed);
                if (match.Success)
                {
                    var includePath = match.Groups[1].Value;
                    var resolvedPath = ResolvePath(normalizedPath, includePath);
                    var directiveLine = i + 1;

                    bodyLines.Add(("", directiveLine));

                    using var includeActivity = OtTraces.Source.Start(OtTraces.Include,
                        (OtTraces.IncludePath, resolvedPath));
                    OtMetrics.IncludesResolved.Add(1);

                    await ResolveRecursiveAsync(
                        resolvedPath,
                        referencedFromFile: normalizedPath,
                        referencedFromLine: directiveLine,
                        sb, sourceMap, currentFrame, cancellationToken);

                    continue;
                }
            }

            inDirectiveSection = false;
            bodyLines.Add((lines[i], i + 1));
        }

        if (bodyLines.Count > 0)
        {
            int spanStart = _currentMergedLine;
            int firstOriginalLine = bodyLines[0].originalLineNumber;

            foreach (var (line, _) in bodyLines)
            {
                sb.AppendLine(line.TrimEnd('\r'));
                _currentMergedLine++;
            }

            sourceMap.AddSpan(spanStart, bodyLines.Count, normalizedPath, firstOriginalLine);
        }
    }

    private static (string File, int Line) DirectiveLocation(
        string? referencedFromFile, int referencedFromLine, string normalizedPath)
    {
        // For the root file (no caller) we report the file itself and line 0,
        // mirroring the historical behaviour of ConfigurationFileNotFoundException.
        if (referencedFromFile is null)
            return (normalizedPath, 0);

        return (referencedFromFile, referencedFromLine);
    }

    private static string ResolvePath(string currentFilePath, string relativePath)
    {
        var dir = currentFilePath.Contains('/')
            ? currentFilePath[..currentFilePath.LastIndexOf('/')]
            : "";

        var resolved = string.IsNullOrEmpty(dir)
            ? relativePath
            : $"{dir}/{relativePath}";

        return resolved.Replace('\\', '/');
    }

    [GeneratedRegex("""^include\s+"([^"]+)"\s*$""")]
    private static partial Regex IncludePattern();

    /// <summary>
    /// Singly linked stack of include paths (root to current) without per-depth list copies.
    /// </summary>
    private sealed class IncludeFrame(IncludeFrame? parent, string path)
    {
        public IncludeFrame? Parent { get; } = parent;
        public string Path { get; } = path;
    }

    private static string FormatIncludeChain(IncludeFrame frame)
    {
        var parts = new List<string>();
        for (IncludeFrame? f=frame; f is not null; f = f.Parent)
            parts.Add(f.Path);
        parts.Reverse();
        return string.Join(" -> ", parts);
    }
}
