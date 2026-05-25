using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio.Views;

/// <summary>
/// Startup window that asks the user how to reach the coordinator. The view
/// model raises <see cref="ConnectionDialogViewModel.Connected"/> on success or
/// <see cref="ConnectionDialogViewModel.QuitRequested"/> on Quit; this code-behind
/// stores the resulting <see cref="CoordinatorConnection"/> in
/// <see cref="Result"/> and closes the window, letting the host await
/// <see cref="ClosedAsync"/> to drive the next step.
/// </summary>
public sealed partial class ConnectionWindow : Window
{
    private readonly ConnectionDialogViewModel _viewModel;
    private readonly TaskCompletionSource<CoordinatorConnection?> _closedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConnectionWindow(ConnectionDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        RootGrid.DataContext = viewModel;

        _viewModel.Connected += OnConnected;
        _viewModel.QuitRequested += OnQuitRequested;

        Closed += OnClosed;

        ApplyBackdrop();

        // Compact dialog footprint; the main shell uses 1280x820 once we hand off.
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(728, 832));

        // Lock the dialog to a single fixed size: there's nothing in the form
        // that benefits from being resized, and pinning the chrome avoids the
        // user accidentally shrinking it below the radio + footer footprint.
        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        Title = "Connect to Tinkwell";
    }

    /// <summary>
    /// Pre-populates the form fields from a previously saved connection.
    /// Called by the host once after construction; the window itself does not
    /// touch the connection store.
    /// </summary>
    public void LoadDefaults(CoordinatorConnection connection) => _viewModel.LoadFrom(connection);

    /// <summary>
    /// Completes with the validated connection on Connect, or <c>null</c> when
    /// the user clicks Quit (or closes the window via the title bar).
    /// </summary>
    public Task<CoordinatorConnection?> ClosedAsync => _closedTcs.Task;

    private CoordinatorConnection? Result { get; set; }

    private void OnConnected(object? sender, CoordinatorConnection connection)
    {
        Result = connection;
        Close();
    }

    private void OnQuitRequested(object? sender, EventArgs e)
    {
        Result = null;
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Detach to prevent leaks; the view model is a transient instance so it
        // becomes garbage as soon as the host releases its reference.
        _viewModel.Connected -= OnConnected;
        _viewModel.QuitRequested -= OnQuitRequested;
        Closed -= OnClosed;

        _closedTcs.TrySetResult(Result);
    }

    private void ApplyBackdrop()
    {
        // Mirror MainWindow's backdrop choice so the dialog reads as part of
        // the same app surface (Mica on Win11, Acrylic fallback on Win10).
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        else if (DesktopAcrylicController.IsSupported())
            SystemBackdrop = new DesktopAcrylicBackdrop();
    }
}
