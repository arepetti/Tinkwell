using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class EventRow : ObservableObject
{
    public EventRow(DateTimeOffset timestamp, string verb, string? source, string? name, JsonElement raw)
    {
        Timestamp = timestamp;
        Verb = verb;
        Source = source;
        Name = name;
        Raw = raw;
    }

    public DateTimeOffset Timestamp { get; }

    public string Verb { get; }

    public string? Source { get; }

    public string? Name { get; }

    public JsonElement Raw { get; }

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    public string Summary => $"{Verb} {Source} {Name}".Trim();

    public IReadOnlyList<Detail> Details
    {
        get
        {
            var list = new List<Detail>
            {
                new("Time", Timestamp.ToLocalTime().ToString("u")),
                new("Verb", Verb),
                new("Source", Source),
                new("Name", Name),
            };
            foreach (var detail in DetailsBuilder.FromElement(Raw, _skipAlreadyShown))
                list.Add(detail);
            return list;
        }
    }

    private static readonly HashSet<string> _skipAlreadyShown = new(StringComparer.Ordinal)
    {
        "verb", "type", "source", "name", "event", "timestamp", "ts", "time", "when"
    };
}

public sealed partial class EventsViewModel : CategoryViewModelBase, IDisposable
{
    /// <summary>
    /// Hard cap on the visible event log. The watcher is always-on so it
    /// would otherwise grow unbounded; capping at a small ring keeps memory
    /// flat and matches the user-facing intent of "show me the recent
    /// events" (older events are still discoverable through <c>tw events
    /// list</c> on demand).
    /// </summary>
    private const int MaxRows = 100;

    private readonly ITwCli _cli;
    // The events watch is owned for the entire lifetime of the view model
    // (a singleton): the user wants events ticking in the background even
    // when the category isn't visible, so OnActivatedAsync /
    // OnDeactivatedAsync intentionally do not touch the stream.
    private readonly CancellationTokenSource _streamCts = new();
    private Task? _streamTask;

    public EventsViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
        StartAlwaysOnStream();
    }

    public override string Title => "Events";

    // Segoe Fluent Icons glyph: LightningBolt (E945).
    public override string Icon => "\uE945";

    public ObservableCollection<EventRow> Events { get; } = new();

    [ObservableProperty]
    private EventRow? _selectedEvent;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string? _verbFilter;

    [ObservableProperty]
    private string? _sourceFilter;

    public bool IsDrawerOpen => SelectedEvent is not null;

    public string? SelectedRawJson => SelectedEvent is null
        ? null
        : JsonSerializer.Serialize(SelectedEvent.Raw, new JsonSerializerOptions { WriteIndented = true });

    [ObservableProperty]
    private string _publishName = string.Empty;

    [ObservableProperty]
    private string _publishVerb = "other";

    [ObservableProperty]
    private string? _publishSource;

    [ObservableProperty]
    private string? _publishObject;

    [ObservableProperty]
    private string? _publishPayload;

    [ObservableProperty]
    private bool _isPublishDialogOpen;

    [ObservableProperty]
    private string? _publishError;

    // OnActivatedAsync / OnDeactivatedAsync intentionally inherit the base
    // no-op behavior: the events watch is always running in the background,
    // so switching to or away from the category must not start or stop it.

    [RelayCommand]
    private void Clear()
    {
        Events.Clear();
        SelectedEvent = null;
    }

    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

    [RelayCommand]
    private void OpenPublishDialog()
    {
        PublishError = null;
        IsPublishDialogOpen = true;
    }

    [RelayCommand]
    private void ClosePublishDialog()
        => IsPublishDialogOpen = false;

    [RelayCommand]
    private async Task SubmitPublishAsync()
    {
        if (string.IsNullOrWhiteSpace(PublishName))
        {
            PublishError = "Name is required.";
            return;
        }

        PublishError = null;

        var args = new List<string> { "events", "publish", PublishName };
        if (!string.IsNullOrWhiteSpace(PublishVerb))
        {
            args.Add("--verb");
            args.Add(PublishVerb);
        }
        if (!string.IsNullOrWhiteSpace(PublishSource))
        {
            args.Add("--source");
            args.Add(PublishSource!);
        }
        if (!string.IsNullOrWhiteSpace(PublishObject))
        {
            args.Add("--object");
            args.Add(PublishObject!);
        }
        if (!string.IsNullOrWhiteSpace(PublishPayload))
        {
            args.Add("--payload");
            args.Add(PublishPayload!);
        }

        try
        {
            await _cli.RunOneShotAsync(args, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                IsPublishDialogOpen = false;
                StatusMessage = $"Published `{PublishName}`.";
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => PublishError = ex.Message);
        }
    }

    [RelayCommand]
    private void CloseDrawer() => SelectedEvent = null;

    partial void OnSelectedEventChanged(EventRow? value)
    {
        OnPropertyChanged(nameof(SelectedRawJson));
        OnPropertyChanged(nameof(IsDrawerOpen));
    }

    private void StartAlwaysOnStream()
    {
        _streamTask = Task.Run(() => ConsumeWithRetryAsync(_streamCts.Token));
    }

    private async Task ConsumeWithRetryAsync(CancellationToken cancellationToken)
    {
        // Keep reconnecting for the lifetime of the VM. The stream can drop
        // for many reasons (coordinator restart, gRPC channel reset, the tw
        // process exiting cleanly) and we don't want the user to silently
        // stop seeing events; an exponential-backoff retry keeps the watch
        // self-healing without busy-looping when the coordinator is down.
        // IsStreaming flips with the actual subscription state so the UI can
        // distinguish "live" from "reconnecting" and the user has a clear
        // signal when the bus is unreachable (e.g. coordinator down).
        var backoff = TimeSpan.FromMilliseconds(500);
        while (!cancellationToken.IsCancellationRequested)
        {
            Dispatcher.Post(() => IsStreaming = true);
            try
            {
                await ConsumeOnceAsync(cancellationToken).ConfigureAwait(false);
                backoff = TimeSpan.FromMilliseconds(500);
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Post(() => IsStreaming = false);
                return;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                Dispatcher.Post(() => SetError(ex));
                backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, 10_000));
            }

            Dispatcher.Post(() => IsStreaming = false);

            try
            {
                await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ConsumeOnceAsync(CancellationToken cancellationToken)
    {
        await foreach (var element in _cli.StreamAsync(new[] { "events", "watch" }, cancellationToken).ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // IsPaused is honored here (and not at the dispatcher hop) so a
            // paused stream still cleanly exits when the VM is disposed:
            // the loop body never blocks on the dispatcher.
            if (IsPaused)
                continue;

            var row = CreateRow(element);
            if (!PassesFilter(row))
                continue;

            Dispatcher.Post(() =>
            {
                // Newest at the top, oldest dropped: the list is a sliding
                // window of the most recent MaxRows events.
                Events.Insert(0, row);
                while (Events.Count > MaxRows)
                    Events.RemoveAt(Events.Count - 1);
            });
        }
    }

    public void Dispose()
    {
        _streamCts.Cancel();
        if (_streamTask is not null)
        {
            try
            {
                _streamTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }
        _streamCts.Dispose();
    }

    private static EventRow CreateRow(JsonElement element)
    {
        var verb = TryGetString(element, "verb") ?? TryGetString(element, "type") ?? "?";
        var source = TryGetString(element, "source");
        var name = TryGetString(element, "name") ?? TryGetString(element, "event");
        var timestamp = ParseTimestamp(element) ?? DateTimeOffset.UtcNow;
        return new EventRow(timestamp, verb, source, name, element);
    }

    private bool PassesFilter(EventRow row)
    {
        if (!string.IsNullOrWhiteSpace(VerbFilter) && !row.Verb.Contains(VerbFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(SourceFilter)
            && (row.Source is null || !row.Source.Contains(SourceFilter, StringComparison.OrdinalIgnoreCase)))
            return false;
        return true;
    }

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? ParseTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var key in new[] { "timestamp", "ts", "time", "when" })
        {
            if (element.TryGetProperty(key, out var v)
                && v.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(v.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }
}
