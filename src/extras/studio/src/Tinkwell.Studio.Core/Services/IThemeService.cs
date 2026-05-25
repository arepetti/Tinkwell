namespace Tinkwell.Studio.Services;

/// <summary>
/// Tracks and toggles the current theme variant. UI hosts implement this by
/// flipping the root ElementTheme / ThemeVariant; the view model only exposes
/// the toggle and the current flag.
/// </summary>
public interface IThemeService
{
    bool IsDark { get; }

    event EventHandler<bool>? Changed;

    void Toggle();
}
