using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class CoapHistoryEntry : ObservableObject
{
    public CoapHistoryEntry(DateTimeOffset timestamp, string method, string path, string host, int port, string? status, string? body)
    {
        Timestamp = timestamp;
        Method = method;
        Path = path;
        Host = host;
        Port = port;
        Status = status;
        Body = body;
    }

    public DateTimeOffset Timestamp { get; }

    public string Method { get; }

    public string Path { get; }

    public string Host { get; }

    public int Port { get; }

    public string? Status { get; }

    public string? Body { get; }

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string Target => $"{Host}:{Port}{Path}";

    public IReadOnlyList<Detail> Details => new List<Detail>
    {
        new("Method", Method.ToUpperInvariant()),
        new("Target", Target, DetailKind.Url),
        new("Host", Host, DetailKind.Url),
        new("Port", Port.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Path", Path),
        new("Time", Timestamp.ToLocalTime().ToString("u")),
        new("Status", Status, DetailKind.Status),
    };
}

public sealed partial class CoapViewModel : CategoryViewModelBase
{
    private static readonly string[] _methods = new[] { "get", "post", "put", "delete" };
    private static readonly string[] _accepts = new[] { "", "text", "json", "binary" };

    private readonly ITwCli _cli;

    public CoapViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "CoAP";

    // Segoe Fluent Icons glyph: Send (E724).
    public override string Icon => "\uE724";

    public IReadOnlyList<string> Methods => _methods;

    public IReadOnlyList<string> AcceptOptions => _accepts;

    public ObservableCollection<CoapHistoryEntry> History { get; } = new();

    [ObservableProperty]
    private string _method = "get";

    [ObservableProperty]
    private string _path = "/";

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private int _port = 5683;

    [ObservableProperty]
    private string? _payloadText;

    [ObservableProperty]
    private string _accept = string.Empty;

    [ObservableProperty]
    private int _timeoutSeconds = 5;

    [ObservableProperty]
    private CoapHistoryEntry? _selected;

    public bool IsDrawerOpen => Selected is not null;

    /// <summary>
    /// Drives the request-builder overlay. Bound fields (Method / Path / Host /
    /// Port / Accept / TimeoutSeconds / PayloadText) are observable properties
    /// on this VM, so the values stick around between open/close cycles and the
    /// user can fire the same request several times without reconfiguring it.
    /// </summary>
    [ObservableProperty]
    private bool _isSettingsOpen;

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var args = new List<string> { "coap", "send", Method, Path, "-H", Host, "--port", Port.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (!string.IsNullOrWhiteSpace(PayloadText))
            {
                args.Add("--payload");
                args.Add(PayloadText!);
            }
            if (!string.IsNullOrWhiteSpace(Accept))
            {
                args.Add("--accept");
                args.Add(Accept);
            }
            if (TimeoutSeconds > 0)
            {
                args.Add("--timeout");
                args.Add(TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            string? status = null;
            string? body = null;

            try
            {
                var response = await _cli.RunOneShotManyAsync(args, cancellationToken).ConfigureAwait(false);
                if (response.Count > 0)
                {
                    var first = response[0];
                    status = TryGetString(first, "status") ?? TryGetString(first, "code");
                    body = TryGetString(first, "body")
                        ?? (first.ValueKind == JsonValueKind.Object
                            ? JsonSerializer.Serialize(first, new JsonSerializerOptions { WriteIndented = true })
                            : first.ToString());
                }
                else
                {
                    status = "ok";
                    body = "(no body)";
                }
            }
            catch (TwCliException ex)
            {
                status = "error";
                body = ex.Stderr;
            }

            var entry = new CoapHistoryEntry(
                DateTimeOffset.UtcNow, Method, Path, Host, Port, status, body);

            Dispatcher.Post(() =>
            {
                History.Insert(0, entry);
                Selected = entry;
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
        finally
        {
            Dispatcher.Post(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void Duplicate(CoapHistoryEntry entry)
    {
        Method = entry.Method;
        Path = entry.Path;
        Host = entry.Host;
        Port = entry.Port;
    }

    [RelayCommand]
    private void ClearHistory() => History.Clear();

    [RelayCommand]
    private void CloseDrawer() => Selected = null;

    partial void OnSelectedChanged(CoapHistoryEntry? value)
        => OnPropertyChanged(nameof(IsDrawerOpen));

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;
}
