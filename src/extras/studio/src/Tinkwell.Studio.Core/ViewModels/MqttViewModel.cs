using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class MqttViewModel : CategoryViewModelBase
{
    private readonly ITwCli _cli;
    private readonly MqttMonitorService _monitor;

    public MqttViewModel(ITwCli cli, MqttMonitorService monitor, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
        _monitor = monitor;
        _monitor.MessageReceived += (_, msg) => Dispatcher.Post(() => AppendMessage(msg));
        _monitor.ConnectionChanged += (_, connected) => Dispatcher.Post(() =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "Connected" : "Disconnected";
        });
    }

    public override string Title => "MQTT";

    // Segoe Fluent Icons glyph: Cloud (E753).
    public override string Icon => "\uE753";

    public ObservableCollection<MqttIncomingMessage> Messages { get; } = new();

    public ObservableCollection<string> Subscriptions { get; } = new();

    [ObservableProperty]
    private string _brokerHost = "localhost";

    [ObservableProperty]
    private int _brokerPort = 1883;

    [ObservableProperty]
    private string? _clientId;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private string _newTopic = "#";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private MqttIncomingMessage? _selectedMessage;

    public bool IsDrawerOpen => SelectedMessage is not null;

    [ObservableProperty]
    private string _publishTopic = string.Empty;

    [ObservableProperty]
    private string _publishPayload = string.Empty;

    [ObservableProperty]
    private int _publishQos;

    [ObservableProperty]
    private bool _publishRetain;

    [ObservableProperty]
    private bool _isPublishOpen;

    [ObservableProperty]
    private string? _publishError;

    /// <summary>
    /// Drives the connection-settings overlay. The bound fields (BrokerHost,
    /// BrokerPort, ClientId, Username, Password) are the same observable
    /// properties the toolbar used to edit, so the values persist across
    /// open/close cycles automatically — no separate "draft" state needed.
    /// </summary>
    [ObservableProperty]
    private bool _isSettingsOpen;

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        ClearError();
        try
        {
            var opts = new MqttConnectionOptions(
                BrokerHost, BrokerPort, ClientId,
                string.IsNullOrWhiteSpace(Username) ? null : Username,
                string.IsNullOrWhiteSpace(Password) ? null : Password);
            await _monitor.ConnectAsync(opts, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _monitor.DisconnectAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task AddSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTopic))
            return;
        try
        {
            await _monitor.SubscribeAsync(NewTopic.Trim(), CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                if (!Subscriptions.Contains(NewTopic.Trim()))
                    Subscriptions.Add(NewTopic.Trim());
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    [RelayCommand]
    private async Task RemoveSubscriptionAsync(string topic)
    {
        try
        {
            await _monitor.UnsubscribeAsync(topic, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() => Subscriptions.Remove(topic));
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    [RelayCommand]
    private void OpenPublish()
    {
        PublishError = null;
        IsPublishOpen = true;
    }

    [RelayCommand]
    private void ClosePublish() => IsPublishOpen = false;

    [RelayCommand]
    private async Task SubmitPublishAsync()
    {
        if (string.IsNullOrWhiteSpace(PublishTopic))
        {
            PublishError = "Topic is required.";
            return;
        }

        var args = new List<string>
        {
            "mqtt", "publish", PublishTopic, PublishPayload,
            "--broker", BrokerHost,
            "--port", BrokerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--qos", PublishQos.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (PublishRetain)
            args.Add("--retain");

        try
        {
            await _cli.RunOneShotAsync(args, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                IsPublishOpen = false;
                StatusMessage = $"Published to `{PublishTopic}`.";
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => PublishError = ex.Message);
        }
    }

    [RelayCommand]
    private async Task PingAsync()
    {
        try
        {
            var args = new[]
            {
                "mqtt", "ping",
                "--broker", BrokerHost,
                "--port", BrokerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            await _cli.RunOneShotAsync(args, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() => StatusMessage = $"Broker reachable: {BrokerHost}:{BrokerPort}.");
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    [RelayCommand]
    private void Clear() => Messages.Clear();

    [RelayCommand]
    private void CloseDrawer() => SelectedMessage = null;

    partial void OnSelectedMessageChanged(MqttIncomingMessage? value)
        => OnPropertyChanged(nameof(IsDrawerOpen));

    private void AppendMessage(MqttIncomingMessage msg)
    {
        Messages.Insert(0, msg);
        while (Messages.Count > 5000)
            Messages.RemoveAt(Messages.Count - 1);
    }
}
