using Microsoft.UI.Xaml;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Flips the root <see cref="FrameworkElement.RequestedTheme"/> between Light and Dark.
/// The application is not restarted; the WinUI theme system propagates the new
/// variant to every theme-resource brush (CardBackgroundFillColorDefaultBrush,
/// TextFillColorPrimaryBrush, ...) live.
/// </summary>
public sealed class WinUiThemeService : IThemeService
{
    private FrameworkElement? _root;
    private bool _isDark;

    public bool IsDark => _isDark;

    public event EventHandler<bool>? Changed;

    /// <summary>
    /// Called once by MainWindow when it attaches its root element. Required
    /// because WinUI has no app-wide "theme variant" knob; we flip the root's
    /// RequestedTheme instead.
    /// </summary>
    public void AttachRoot(FrameworkElement root)
    {
        _root = root;
        // Seed our state from the detected system theme.
        _isDark = root.ActualTheme == ElementTheme.Dark;
    }

    public void Toggle()
    {
        if (_root is null)
            return;

        var next = _isDark ? ElementTheme.Light : ElementTheme.Dark;
        _root.RequestedTheme = next;
        _isDark = next == ElementTheme.Dark;
        Changed?.Invoke(this, _isDark);
    }
}
