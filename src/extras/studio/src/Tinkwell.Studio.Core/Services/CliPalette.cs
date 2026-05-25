namespace Tinkwell.Studio.Services;

/// <summary>
/// Semantic classification of a value displayed in the UI. The view layer maps
/// this into a theme-appropriate brush; keeping the enum in Core keeps the
/// view models free of UI-framework types.
/// </summary>
public enum DetailSemantic
{
    None,
    Url,
    Ok,
    Bad,
    Warn,
}

/// <summary>
/// Mirrors the semantic colors used by the `tw` CLI (see
/// <c>Tinkwell.Cli.Sdk.OutputContext.FormatCell</c>): status keywords are green/red/yellow,
/// URLs / paths / IP endpoints are magenta. Values are exposed as hex strings; the view
/// layer wraps them into SolidColorBrushes appropriate for the host UI framework.
/// </summary>
public static class CliPalette
{
    // Colors picked to read well on both Light and Dark Fluent themes.
    public const string UrlHex = "#C57DD4";   // magenta-ish
    public const string OkHex = "#4CAF50";    // green
    public const string BadHex = "#E05E5E";   // red
    public const string WarnHex = "#E0A800";  // amber
    public const string NeutralHex = "#888888";

    /// <summary>
    /// Returns the CLI semantic classification of a status keyword, or
    /// <see cref="DetailSemantic.None"/> so callers can fall back to the theme default.
    /// </summary>
    public static DetailSemantic ClassifyStatus(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return DetailSemantic.None;

        return value.ToLowerInvariant() switch
        {
            "ready" or "running" or "ok" or "healthy" => DetailSemantic.Ok,
            "crashed" or "fatal" or "error" or "unhealthy" => DetailSemantic.Bad,
            "starting" or "restarting" or "waitingforready" or "degraded" or "unknown" => DetailSemantic.Warn,
            _ => DetailSemantic.None,
        };
    }

    /// <summary>
    /// Returns the hex color for a status keyword, or <c>null</c> if the status isn't
    /// a known one (so the view keeps its theme-inherited foreground).
    /// </summary>
    public static string? StatusToHex(string? value)
        => ClassifyStatus(value) switch
        {
            DetailSemantic.Ok => OkHex,
            DetailSemantic.Bad => BadHex,
            DetailSemantic.Warn => WarnHex,
            _ => null,
        };

    public static bool LooksLikeUrlOrHost(string value)
    {
        if (value.Contains("://", System.StringComparison.Ordinal))
            return true;

        if (value.Contains('/') || value.Contains('\\'))
            return true;

        // host:port where host is an IP
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < value.Length - 1)
        {
            if (System.Net.IPAddress.TryParse(value.AsSpan(0, colonIndex), out _)
                && int.TryParse(value.AsSpan(colonIndex + 1), out _))
                return true;
        }

        return System.Net.IPAddress.TryParse(value, out _);
    }
}
