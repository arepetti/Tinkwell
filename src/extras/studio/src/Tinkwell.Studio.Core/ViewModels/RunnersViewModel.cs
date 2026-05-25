using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class RunnerRow : ObservableObject
{
    public RunnerRow(string name, string? id, string? status, int? pid, string? endpoint, JsonElement raw)
    {
        Name = name;
        Id = id;
        Status = status;
        Pid = pid;
        Endpoint = endpoint;
        Raw = raw;
    }

    public string Name { get; }

    public string? Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(Tone))]
    private string? _status;

    public int? Pid { get; }

    public string? Endpoint { get; }

    public JsonElement Raw { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(Tone))]
    private string? _healthStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthCpuPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthMemory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthThreads;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthHandles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthChecks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _healthUpdatedAt;

    /// <summary>
    /// <see cref="IndicatorTone"/> for the row chip. Combines <see cref="Status"/>
    /// and <see cref="HealthStatus"/> so the worst signal wins: a runner with
    /// status "running" but a "crashed" health check still flags critical, and
    /// a runner whose status is unrecognized (or literally "stopped") is
    /// treated as critical because that's the user-facing meaning of "not up".
    /// </summary>
    public IndicatorTone Tone => ClassifyTone(Status, HealthStatus);

    internal static IndicatorTone ClassifyTone(string? status, string? health)
    {
        var statusSemantic = CliPalette.ClassifyStatus(status);
        var healthSemantic = CliPalette.ClassifyStatus(health);

        if (statusSemantic == DetailSemantic.Bad || healthSemantic == DetailSemantic.Bad)
            return IndicatorTone.Critical;

        if (statusSemantic == DetailSemantic.Warn || healthSemantic == DetailSemantic.Warn)
            return IndicatorTone.Warning;

        if (statusSemantic == DetailSemantic.Ok)
        {
            // Promote to Success only when health agrees (or hasn't been
            // reported yet); a degraded/unknown health drops us back to
            // Warning above so we don't end up here with a non-Ok health.
            return IndicatorTone.Success;
        }

        // Anything else — empty, "stopped", "exited", or a keyword the
        // palette doesn't recognize — counts as "not up": red per the spec.
        return string.IsNullOrEmpty(status) ? IndicatorTone.Default : IndicatorTone.Critical;
    }

    public IReadOnlyList<Detail> Details
    {
        get
        {
            var list = new List<Detail>
            {
                new("Name", Name),
                new("Id", Id),
                new("Status", Status, DetailKind.Status),
                new("PID", Pid?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("Endpoint", Endpoint, DetailKind.Url),
                new("Health", HealthStatus, DetailKind.Status),
            };

            if (!string.IsNullOrEmpty(HealthCpuPercent))
                list.Add(new Detail("CPU", $"{HealthCpuPercent}%", DetailKind.Number));
            if (!string.IsNullOrEmpty(HealthMemory))
                list.Add(new Detail("Memory", HealthMemory, DetailKind.Number));
            if (!string.IsNullOrEmpty(HealthThreads))
                list.Add(new Detail("Threads", HealthThreads, DetailKind.Number));
            if (!string.IsNullOrEmpty(HealthHandles))
                list.Add(new Detail("Handles", HealthHandles, DetailKind.Number));
            if (!string.IsNullOrEmpty(HealthChecks))
                list.Add(new Detail("Checks", HealthChecks));
            if (!string.IsNullOrEmpty(HealthUpdatedAt))
                list.Add(new Detail("Health updated", HealthUpdatedAt));

            foreach (var detail in DetailsBuilder.FromElement(Raw, _skipAlreadyShown))
                list.Add(detail);
            return list;
        }
    }

    private static readonly HashSet<string> _skipAlreadyShown = new(StringComparer.Ordinal)
    {
        "name", "id", "status", "state", "pid", "processId", "endpoint"
    };
}

public sealed partial class RunnersViewModel : CategoryViewModelBase
{
    private readonly ITwCli _cli;
    // Master snapshot from the most recent refresh; the visible collection
    // is rebuilt from it whenever the filter text changes.
    private readonly List<RunnerRow> _allRunners = new();

    public RunnersViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Runners";

    // Segoe Fluent Icons glyph: Play (E768).
    public override string Icon => "\uE768";

    public override bool SupportsSearch => true;

    public override string SearchPlaceholder => "Filter by name or id";

    public ObservableCollection<RunnerRow> Runners { get; } = new();

    [ObservableProperty]
    private RunnerRow? _selected;

    public bool IsDrawerOpen => Selected is not null;

    public string? SelectedRawJson => Selected is null
        ? null
        : JsonSerializer.Serialize(Selected.Raw, new JsonSerializerOptions { WriteIndented = true });

    protected override void OnSearchTextUpdated(string value) => ApplyFilter();

    private bool MatchesFilter(RunnerRow row)
    {
        var f = SearchText;
        if (string.IsNullOrWhiteSpace(f))
            return true;
        return row.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || (row.Id is not null && row.Id.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyFilter()
    {
        Runners.Clear();
        foreach (var r in _allRunners)
        {
            if (MatchesFilter(r))
                Runners.Add(r);
        }
        if (Selected is not null && !MatchesFilter(Selected))
            Selected = null;
    }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
        => await RefreshAsync(cancellationToken);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var list = await _cli.RunOneShotManyAsync(new[] { "runners", "list" }, cancellationToken).ConfigureAwait(false);
            var health = await SafeHealthAsync(cancellationToken).ConfigureAwait(false);

            var rows = list.Select(e => CreateRow(e, health)).ToList();
            Dispatcher.Post(() =>
            {
                _allRunners.Clear();
                _allRunners.AddRange(rows);
                ApplyFilter();
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
    private void CloseDrawer() => Selected = null;

    partial void OnSelectedChanged(RunnerRow? value)
    {
        OnPropertyChanged(nameof(IsDrawerOpen));
        OnPropertyChanged(nameof(SelectedRawJson));
    }

    /// <summary>
    /// Snapshot of a row returned by <c>tw runners health --verbose</c>. Columns map
    /// 1:1 to <see cref="Tinkwell.Cli.Commands.Coordinator.RunnersHealthCommand"/>
    /// (serialized as camelCase by the CLI JSONL writer).
    /// </summary>
    private sealed record HealthSnapshot(
        string? Status,
        string? CpuPercent,
        string? Memory,
        string? Threads,
        string? Handles,
        string? Checks,
        string? Timestamp);

    private async Task<Dictionary<string, HealthSnapshot>> SafeHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var many = await _cli.RunOneShotManyAsync(new[] { "runners", "health" }, cancellationToken).ConfigureAwait(false);
            var map = new Dictionary<string, HealthSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in many)
            {
                var name = TryGetString(entry, "runner") ?? TryGetString(entry, "name");
                if (name is null)
                    continue;

                map[name] = new HealthSnapshot(
                    Status: TryGetString(entry, "status") ?? TryGetString(entry, "health"),
                    CpuPercent: NormalizeMissing(TryGetString(entry, "cpuPercent")),
                    Memory: NormalizeMissing(TryGetString(entry, "memory")),
                    Threads: NormalizeMissing(TryGetString(entry, "threads")),
                    Handles: NormalizeMissing(TryGetString(entry, "handles")),
                    Checks: NormalizeMissing(TryGetString(entry, "checks")),
                    Timestamp: NormalizeMissing(TryGetString(entry, "timestamp")));
            }
            return map;
        }
        catch
        {
            return new Dictionary<string, HealthSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static RunnerRow CreateRow(JsonElement element, IReadOnlyDictionary<string, HealthSnapshot> health)
    {
        var name = TryGetString(element, "name") ?? "(unnamed)";
        var id = TryGetString(element, "id");
        var status = TryGetString(element, "status") ?? TryGetString(element, "state");
        var pid = TryGetInt(element, "processId") ?? TryGetInt(element, "pid");
        var endpoint = TryGetString(element, "endpoint");
        var row = new RunnerRow(name, id, status, pid, endpoint, element);
        if (health.TryGetValue(name, out var h))
        {
            row.HealthStatus = h.Status;
            row.HealthCpuPercent = h.CpuPercent;
            row.HealthMemory = h.Memory;
            row.HealthThreads = h.Threads;
            row.HealthHandles = h.Handles;
            row.HealthChecks = h.Checks;
            row.HealthUpdatedAt = h.Timestamp;
        }
        return row;
    }

    /// <summary>The CLI emits "-" for missing numeric cells; treat it as null.</summary>
    private static string? NormalizeMissing(string? value)
        => string.IsNullOrEmpty(value) || value == "-" ? null : value;

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? TryGetInt(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}
