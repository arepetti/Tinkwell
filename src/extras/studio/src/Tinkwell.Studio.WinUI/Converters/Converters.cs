using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;
using Windows.UI;

namespace Tinkwell.Studio.Converters;

/// <summary>
/// Parses a <c>#RRGGBB</c> hex string (as produced by the Core palette) into a
/// <see cref="SolidColorBrush"/>. Falls back to a neutral grey on parse errors
/// so XAML never sees a null brush.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && TryParseHex(hex, out var color))
            return new SolidColorBrush(color);
        return new SolidColorBrush(Color.FromArgb(0xFF, 0x88, 0x88, 0x88));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    internal static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex))
            return false;

        var span = hex.AsSpan();
        if (span[0] == '#')
            span = span[1..];

        if (span.Length != 6 && span.Length != 8)
            return false;

        byte a = 0xFF;
        var offset = 0;
        if (span.Length == 8)
        {
            if (!byte.TryParse(span.Slice(0, 2), System.Globalization.NumberStyles.HexNumber, null, out a))
                return false;
            offset = 2;
        }

        if (!byte.TryParse(span.Slice(offset, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(span.Slice(offset + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(span.Slice(offset + 4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return false;

        color = Color.FromArgb(a, r, g, b);
        return true;
    }
}

/// <summary>
/// Binds a status string (ready / running / crashed / ...) to a palette brush.
/// For values that don't match a known CLI status keyword, returns <see cref="DependencyProperty.UnsetValue"/>
/// so the target keeps its theme-inherited foreground.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var hex = CliPalette.StatusToHex(value as string);
        if (hex is null || !HexToBrushConverter.TryParseHex(hex, out var color))
            return DependencyProperty.UnsetValue;
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Binds a URL / host / path string to the palette's URL brush when the value
/// actually looks like one. Everything else falls back to the theme default.
/// </summary>
public sealed class UrlToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && !string.IsNullOrEmpty(s) && CliPalette.LooksLikeUrlOrHost(s)
            && HexToBrushConverter.TryParseHex(CliPalette.UrlHex, out var color))
            return new SolidColorBrush(color);
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Binds a <see cref="Detail"/> (or a raw string) to its foreground brush.
/// Detail objects already carry the semantic classification (URL / status /
/// plain), so the converter only has to pick the palette entry.
/// </summary>
public sealed class DetailForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Detail d)
        {
            var hex = d.Semantic switch
            {
                DetailSemantic.Url => CliPalette.UrlHex,
                DetailSemantic.Ok => CliPalette.OkHex,
                DetailSemantic.Bad => CliPalette.BadHex,
                DetailSemantic.Warn => CliPalette.WarnHex,
                _ => null,
            };
            if (hex is not null && HexToBrushConverter.TryParseHex(hex, out var color))
                return new SolidColorBrush(color);
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Picks a monospace font for detail rows that carry raw JSON / hex payloads,
/// and the theme default for everything else. Mirrors the CLI's "raw cell" style.
/// </summary>
public sealed class DetailFontFamilyConverter : IValueConverter
{
    private static readonly FontFamily Monospace = new("Cascadia Code, Consolas, Menlo, monospace");

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Detail d && d.IsMonospace ? Monospace : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Collapses UI affordances when a bound string is null or empty.
/// WinUI has no built-in equivalent of Avalonia's StringConverters.IsNotNullOrEmpty.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Visible when the bound boolean is true. Frequently used to gate drawers and
/// status chips by a VM flag.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean for <c>IsEnabled</c> bindings (WinUI has no bang-operator
/// shortcut in XAML). Equivalent to Avalonia's <c>{Binding !Foo}</c>.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : false;
}

/// <summary>
/// Visible when the bound <see cref="int"/> is greater than zero. Used for
/// empty-state placeholders (e.g. "no status slices yet").
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Visible when the bound value is not null. Needed because WinUI's default
/// null-handling for data templates collapses neither the outer container nor
/// the placeholder.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverse of <see cref="NullToVisibilityConverter"/>: visible when the bound
/// value <em>is</em> null. Used by <c>WorkspacePage</c> to swap a custom drawer
/// header in for the default title TextBlock.
/// </summary>
public sealed class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a <see cref="MeasureKind"/> to a Segoe Fluent Icons glyph for the
/// per-row indicator in the measures table. <c>Normal</c> resolves to an
/// empty string so the cell renders blank instead of leaking a placeholder.
/// </summary>
public sealed class MeasureKindToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is MeasureKind kind ? KindToGlyph(kind) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    internal static string KindToGlyph(MeasureKind kind) => kind switch
    {
        // Lock: pinned / cannot change after first set.
        MeasureKind.Constant => "\uE72E",
        // Code: an expression evaluated against other measures.
        MeasureKind.Calculated => "\uE943",
        // Settings cog: surfaced internally by the runtime.
        MeasureKind.System => "\uE713",
        _ => string.Empty,
    };
}

/// <summary>
/// Maps a <see cref="MeasureKind"/> to a short human label, used as the
/// tooltip for the kind icon column.
/// </summary>
public sealed class MeasureKindToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is MeasureKind kind ? KindToLabel(kind) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    internal static string KindToLabel(MeasureKind kind) => kind switch
    {
        MeasureKind.Constant => "Constant",
        MeasureKind.Calculated => "Calculated",
        MeasureKind.System => "System",
        _ => "Normal",
    };
}
