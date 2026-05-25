using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.Controls;

/// <summary>
/// Tiny "avatar"-style chip used in the per-row leading column of the
/// Studio category lists: a small colored circle with an optional Segoe
/// Fluent glyph in the middle. Two dependency properties drive it:
/// <see cref="Glyph"/> (the glyph string, can be empty for a plain dot)
/// and <see cref="Tone"/> (the semantic color, mapped to a pair of theme
/// brushes by the visual states declared in the XAML template).
/// </summary>
/// <remarks>
/// The control is intentionally a plain <see cref="UserControl"/> rather
/// than a templated <see cref="Control"/>: only Studio uses it, the
/// markup never needs reskinning, and binding tone changes through a
/// VisualStateGroup keeps the theme-resource lookups live (a converter
/// would freeze the resolved <c>SolidColorBrush</c> at the moment of
/// conversion and miss runtime theme switches).
/// </remarks>
public sealed partial class Indicator : UserControl
{
    public Indicator()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateToneVisualState(useTransitions: false);
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(Indicator),
            new PropertyMetadata(string.Empty));

    /// <summary>Segoe Fluent Icons glyph rendered in the middle of the
    /// chip. Empty / null leaves the chip as a plain colored circle, which
    /// is the default for views that have nothing to differentiate.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(IndicatorTone), typeof(Indicator),
            new PropertyMetadata(IndicatorTone.Default, OnToneChanged));

    /// <summary>Semantic color of the chip. The XAML template binds each
    /// value to a pair of theme brushes (background fill + glyph
    /// foreground) through a <c>VisualStateGroup</c>.</summary>
    public IndicatorTone Tone
    {
        get => (IndicatorTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(Indicator),
            new PropertyMetadata(22.0));

    /// <summary>Outer diameter of the chip in DIPs. Defaults to 22 which
    /// fits comfortably in a <c>TableView</c> row at the standard 32px row
    /// height.</summary>
    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public static readonly DependencyProperty GlyphSizeProperty =
        DependencyProperty.Register(nameof(GlyphSize), typeof(double), typeof(Indicator),
            new PropertyMetadata(12.0));

    /// <summary>Font size of the glyph rendered inside the chip. Tuned so
    /// a 12px glyph reads well inside the default 22px circle.</summary>
    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Indicator indicator)
            indicator.UpdateToneVisualState(useTransitions: true);
    }

    private void UpdateToneVisualState(bool useTransitions)
    {
        // VisualStateManager.GoToState walks up to the nearest Control that
        // owns the named state group (the UserControl itself in our case).
        VisualStateManager.GoToState(this, Tone.ToString(), useTransitions);
    }
}
