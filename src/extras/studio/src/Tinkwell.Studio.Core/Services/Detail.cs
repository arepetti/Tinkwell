using System.Text.Json;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Classification used by <see cref="Detail"/> to pick a foreground brush and a font.
/// Mirrors the CLI's semantic cell formatter.
/// </summary>
public enum DetailKind
{
    Default,
    Url,
    Status,
    Number,
    Monospace,
}

/// <summary>
/// One "label: value" row rendered inside a drawer. Colors follow the CLI palette rules
/// (see <see cref="CliPalette"/>).
/// </summary>
public sealed class Detail
{
    public Detail(string label, string? value, DetailKind kind = DetailKind.Default)
    {
        Label = label;
        Value = value;
        Kind = kind;
    }

    public string Label { get; }

    public string? Value { get; }

    public DetailKind Kind { get; }

    /// <summary>
    /// Semantic classification of this detail's value. View-layer converters bind this
    /// into a theme-appropriate brush. Returns <see cref="DetailSemantic.None"/> for
    /// values that should render with the theme default foreground.
    /// </summary>
    public DetailSemantic Semantic => Kind switch
    {
        DetailKind.Url => string.IsNullOrEmpty(Value) || !CliPalette.LooksLikeUrlOrHost(Value)
            ? DetailSemantic.None
            : DetailSemantic.Url,
        DetailKind.Status => CliPalette.ClassifyStatus(Value),
        _ => DetailSemantic.None,
    };

    public bool IsMonospace => Kind == DetailKind.Monospace;
}

/// <summary>
/// Builds <see cref="Detail"/> collections from raw JSON payloads returned by `tw`.
/// </summary>
public static class DetailsBuilder
{
    /// <summary>
    /// Enumerates the top-level properties of <paramref name="element"/> as details, in the
    /// order they appear. Arrays are joined by commas; nested objects are serialized as
    /// one-line JSON and shown monospaced.
    /// </summary>
    public static IReadOnlyList<Detail> FromElement(JsonElement element, IReadOnlyCollection<string>? skip = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return System.Array.Empty<Detail>();

        var list = new List<Detail>();
        foreach (var prop in element.EnumerateObject())
        {
            if (skip is not null && skip.Contains(prop.Name))
                continue;

            var label = Humanize(prop.Name);
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    list.Add(ClassifyString(label, prop.Name, prop.Value.GetString()));
                    break;
                case JsonValueKind.Number:
                    list.Add(new Detail(label, prop.Value.GetRawText(), DetailKind.Number));
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    list.Add(new Detail(label, prop.Value.GetBoolean().ToString()));
                    break;
                case JsonValueKind.Null:
                    list.Add(new Detail(label, null));
                    break;
                case JsonValueKind.Array:
                    list.Add(new Detail(label, JoinArray(prop.Value)));
                    break;
                case JsonValueKind.Object:
                    list.Add(new Detail(label, prop.Value.GetRawText(), DetailKind.Monospace));
                    break;
            }
        }
        return list;
    }

    private static Detail ClassifyString(string label, string propertyName, string? value)
    {
        var lowerName = propertyName.ToLowerInvariant();
        if (lowerName is "url" || lowerName is "endpoint" || lowerName is "host"
            || lowerName.EndsWith("url", System.StringComparison.Ordinal)
            || lowerName.EndsWith("endpoint", System.StringComparison.Ordinal))
        {
            return new Detail(label, value, DetailKind.Url);
        }

        if (lowerName is "status" or "state" or "health" or "healthstatus")
            return new Detail(label, value, DetailKind.Status);

        return new Detail(label, value);
    }

    private static string JoinArray(JsonElement element)
    {
        var parts = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            parts.Add(item.ValueKind switch
            {
                JsonValueKind.String => item.GetString() ?? string.Empty,
                JsonValueKind.Number => item.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => item.GetRawText(),
            });
        }
        return parts.Count == 0 ? "(empty)" : string.Join(", ", parts);
    }

    private static string Humanize(string camelCaseName)
    {
        if (string.IsNullOrEmpty(camelCaseName))
            return camelCaseName;

        var sb = new System.Text.StringBuilder(camelCaseName.Length + 4);
        for (var i = 0; i < camelCaseName.Length; ++i)
        {
            var ch = camelCaseName[i];
            if (i == 0)
            {
                sb.Append(char.ToUpper(ch, System.Globalization.CultureInfo.InvariantCulture));
                continue;
            }
            if (char.IsUpper(ch))
                sb.Append(' ');
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
