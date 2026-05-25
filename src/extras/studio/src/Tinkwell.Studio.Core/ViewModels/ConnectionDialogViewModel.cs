using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

/// <summary>
/// Drives the startup connection dialog. Holds one set of fields per transport
/// variant, exposes per-variant visibility flags for the XAML, and runs the
/// probe when the user clicks Connect. On success it raises <see cref="Connected"/>
/// with the chosen <see cref="CoordinatorConnection"/>; on Quit it raises
/// <see cref="QuitRequested"/>. The view is responsible for closing itself in
/// response to either event.
/// </summary>
public sealed partial class ConnectionDialogViewModel : ObservableObject
{
    private readonly ICoordinatorProbe _probe;

    public ConnectionDialogViewModel(ICoordinatorProbe probe)
    {
        _probe = probe;
    }

    /// <summary>Raised when a successful <c>tw ping</c> validates the chosen connection.</summary>
    public event EventHandler<CoordinatorConnection>? Connected;

    /// <summary>Raised when the user clicks Quit (host should exit the app).</summary>
    public event EventHandler? QuitRequested;

    // Default pipe name kept in sync with TwCoordinatorSettings.PipeName so the
    // user sees the same name the CLI uses out of the box.
    public const string DefaultPipeName = "tinkwell-coordinator";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocalDefault))]
    [NotifyPropertyChangedFor(nameof(IsLocalCustomPipe))]
    [NotifyPropertyChangedFor(nameof(IsRemote))]
    [NotifyPropertyChangedFor(nameof(IsDocker))]
    private CoordinatorTransport _selectedTransport = CoordinatorTransport.LocalDefault;

    [ObservableProperty]
    private string _localPipeName = DefaultPipeName;

    [ObservableProperty]
    private string _remoteMachine = string.Empty;

    [ObservableProperty]
    private string _remotePipeName = DefaultPipeName;

    [ObservableProperty]
    private string _dockerContainer = "tinkwell";

    [ObservableProperty]
    private bool _useDockerCompose;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// True when an error message is set. Exposed as a bool so the XAML can
    /// drive <c>InfoBar.IsOpen</c> without needing a string-to-bool converter.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Each flag is bound TwoWay to a RadioButton's IsChecked. The setter
    // promotes a `true` write to a transport change; `false` is ignored on
    // purpose because RadioButton groups raise IsChecked=false on the
    // previously-selected sibling whenever a new radio is picked, and acting on
    // that would clear the new selection a moment after it was set. The
    // NotifyPropertyChangedFor decorators on SelectedTransport keep all four
    // getters in sync whenever the transport changes via any path.

    public bool IsLocalDefault
    {
        get => SelectedTransport == CoordinatorTransport.LocalDefault;
        set { if (value) SelectedTransport = CoordinatorTransport.LocalDefault; }
    }

    public bool IsLocalCustomPipe
    {
        get => SelectedTransport == CoordinatorTransport.LocalCustomPipe;
        set { if (value) SelectedTransport = CoordinatorTransport.LocalCustomPipe; }
    }

    public bool IsRemote
    {
        get => SelectedTransport == CoordinatorTransport.Remote;
        set { if (value) SelectedTransport = CoordinatorTransport.Remote; }
    }

    public bool IsDocker
    {
        get => SelectedTransport == CoordinatorTransport.Docker;
        set { if (value) SelectedTransport = CoordinatorTransport.Docker; }
    }

    /// <summary>
    /// Populates the form from a previously saved connection. Empty / missing
    /// values fall back to friendly defaults so the user always sees a working
    /// starting point.
    /// </summary>
    public void LoadFrom(CoordinatorConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        SelectedTransport = connection.Transport;

        if (!string.IsNullOrWhiteSpace(connection.PipeName))
        {
            if (connection.Transport == CoordinatorTransport.Remote)
                RemotePipeName = connection.PipeName!;
            else
                LocalPipeName = connection.PipeName!;
        }

        if (!string.IsNullOrWhiteSpace(connection.Machine))
            RemoteMachine = connection.Machine!;

        if (!string.IsNullOrWhiteSpace(connection.DockerContainer))
            DockerContainer = connection.DockerContainer!;

        UseDockerCompose = connection.UseDockerCompose;
    }

    /// <summary>
    /// Snapshots the form into a <see cref="CoordinatorConnection"/>. Only the
    /// fields relevant to the selected transport are carried over; the rest are
    /// nulled out so the persisted payload is minimal.
    /// </summary>
    public CoordinatorConnection BuildConnection() => SelectedTransport switch
    {
        CoordinatorTransport.LocalDefault => new CoordinatorConnection(
            CoordinatorTransport.LocalDefault, null, null, null, false),

        CoordinatorTransport.LocalCustomPipe => new CoordinatorConnection(
            CoordinatorTransport.LocalCustomPipe,
            string.IsNullOrWhiteSpace(LocalPipeName) ? null : LocalPipeName.Trim(),
            null, null, false),

        CoordinatorTransport.Remote => new CoordinatorConnection(
            CoordinatorTransport.Remote,
            string.IsNullOrWhiteSpace(RemotePipeName) ? null : RemotePipeName.Trim(),
            string.IsNullOrWhiteSpace(RemoteMachine) ? null : RemoteMachine.Trim(),
            null, false),

        CoordinatorTransport.Docker => new CoordinatorConnection(
            CoordinatorTransport.Docker,
            null, null,
            string.IsNullOrWhiteSpace(DockerContainer) ? null : DockerContainer.Trim(),
            UseDockerCompose),

        _ => CoordinatorConnection.LocalDefault,
    };

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        if (!TryValidateInputs(out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var candidate = BuildConnection();
            var result = await _probe.PingAsync(candidate, cancellationToken).ConfigureAwait(true);
            if (result.Success)
            {
                Connected?.Invoke(this, candidate);
                return;
            }

            ErrorMessage = result.Error ?? "Could not reach the coordinator.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Per-transport required-field check. Returns <c>false</c> + a friendly
    /// reason when the form is incomplete so we don't even hit the probe.
    /// </summary>
    private bool TryValidateInputs(out string? error)
    {
        switch (SelectedTransport)
        {
            case CoordinatorTransport.LocalCustomPipe:
                if (string.IsNullOrWhiteSpace(LocalPipeName))
                {
                    error = "Enter the pipe name.";
                    return false;
                }
                break;

            case CoordinatorTransport.Remote:
                if (string.IsNullOrWhiteSpace(RemoteMachine))
                {
                    error = "Enter the remote machine name.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(RemotePipeName))
                {
                    error = "Enter the pipe name.";
                    return false;
                }
                break;

            case CoordinatorTransport.Docker:
                if (string.IsNullOrWhiteSpace(DockerContainer))
                {
                    error = "Enter the Docker container name.";
                    return false;
                }
                break;
        }

        error = null;
        return true;
    }
}
