using System.ComponentModel;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio;

/// <summary>
/// Main shell window. Applies Mica as the system backdrop, extends the client
/// area into the title bar, and wires the view model as the root
/// <see cref="FrameworkElement.DataContext"/> so the declarative bindings in
/// XAML resolve.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly WinUiThemeService? _themeService;

    public MainWindow(MainWindowViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _themeService = themeService as WinUiThemeService;

        RootGrid.DataContext = viewModel;

        ApplyMicaBackdrop();
        ConfigureTitleBar();

        _themeService?.AttachRoot(RootGrid);

        // Mirror the selected category onto Window.Title so it shows up in
        // taskbar previews and Alt+Tab in addition to the in-window chrome.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateWindowTitle();

        // Friendly default size; WinUI 3 opens windows small by default.
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedCategory))
            UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var category = _viewModel.SelectedCategory?.Title;
        Title = string.IsNullOrEmpty(category)
            ? "Tinkwell Studio"
            : $"Tinkwell Studio · {category}";
    }

    private void ApplyMicaBackdrop()
    {
        // Mica is supported on Windows 11 22H2+. When unsupported (Windows 10,
        // early Windows 11 builds), fall back to DesktopAcrylic so the window
        // still renders with a theme-appropriate translucent surface.
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void ConfigureTitleBar()
    {
        // Extending into the title bar lets Mica flow edge-to-edge and our
        // custom AppTitleBar Grid host the drag region + status chip + theme
        // toggle. WinUI keeps the standard min/max/close caption buttons on
        // the right automatically.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }
}
