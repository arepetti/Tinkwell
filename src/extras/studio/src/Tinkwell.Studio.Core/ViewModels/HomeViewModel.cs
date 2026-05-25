using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

/// <summary>
/// A single status slice rendered in the breakdown strip: label, count and the
/// semantic color pulled from <see cref="CliPalette"/> so the UI matches the CLI.
/// </summary>
public sealed record StatusSlice(string Label, int Count, string ColorHex);

/// <summary>
/// A runner as shown in the Home resource-usage table. Numeric columns are
/// exposed both as typed values (for <c>ProgressBar.Value</c> bindings) and as
/// pre-formatted strings (for display alongside the bar).
/// </summary>
public sealed class ResourceRow
{
    public required string Name { get; init; }
    public string? Status { get; init; }
    public double CpuPercent { get; init; }
    public string CpuText { get; init; } = "-";
    public long MemoryBytes { get; init; }
    public string MemoryText { get; init; } = "-";
    public string? Threads { get; init; }

    /// <summary>Shared maximum across the current snapshot. Stamped on every row
    /// by the view model so the Memory <c>ProgressBar</c> can bind it without
    /// reaching out to an ancestor (DataGrid cells do not reliably resolve
    /// <c>$parent[UserControl]</c>).</summary>
    public long MaxMemoryBytes { get; set; } = 1;
}

public sealed partial class HomeViewModel : CategoryViewModelBase
{
    private readonly ITwCli _cli;

    public HomeViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Home";

    // Segoe Fluent Icons glyph: Home (E80F).
    public override string Icon => "\uE80F";

    [ObservableProperty]
    private string? _coordinatorStateText;

    [ObservableProperty]
    private bool? _coordinatorReachable;

    [ObservableProperty]
    private int? _runnerCount;

    [ObservableProperty]
    private string? _runnerBreakdown;

    [ObservableProperty]
    private string? _healthySummary;

    [ObservableProperty]
    private string? _pingLatencyText;

    [ObservableProperty]
    private int? _serviceCount;

    [ObservableProperty]
    private int? _storeCount;

    [ObservableProperty]
    private int? _measureCount;

    [ObservableProperty]
    private string? _productName;

    [ObservableProperty]
    private string? _productVersion;

    [ObservableProperty]
    private string? _architecture;

    [ObservableProperty]
    private string? _baseDirectory;

    [ObservableProperty]
    private string? _lastRefreshedText;

    public ObservableCollection<string> PluginRoots { get; } = new();

    public ObservableCollection<string> Extensions { get; } = new();

    public ObservableCollection<StatusSlice> StatusBreakdown { get; } = new();

    public ObservableCollection<ResourceRow> Resources { get; } = new();

    /// <summary>Used as <c>Maximum</c> for the Memory <c>ProgressBar</c> so every row
    /// is drawn relative to the biggest runner. Never zero so bars render.</summary>
    [ObservableProperty]
    private double _maxMemoryBytes = 1;

    [ObservableProperty]
    private string? _rawStatusJson;

    [ObservableProperty]
    private string? _rawInfoJson;

    [ObservableProperty]
    private bool _isDrawerOpen;

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
        => await RefreshAsync(cancellationToken);

    [RelayCommand]
    private async Task Refresh(CancellationToken cancellationToken)
        => await RefreshAsync(cancellationToken);

    [RelayCommand]
    private void ShowRaw() => IsDrawerOpen = true;

    [RelayCommand]
    private void CloseDrawer() => IsDrawerOpen = false;

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ClearError();
        try
        {
            await LoadStatusAsync(cancellationToken).ConfigureAwait(false);
            await LoadPingAsync(cancellationToken).ConfigureAwait(false);
            await LoadInfoAsync(cancellationToken).ConfigureAwait(false);
            await LoadResourceUsageAsync(cancellationToken).ConfigureAwait(false);
            await LoadInventoryCountsAsync(cancellationToken).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                LastRefreshedText = $"Updated {DateTime.Now:T}";
            });
        }
        catch (OperationCanceledException)
        {
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

    private async Task LoadStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _cli.RunOneShotAsync(new[] { "status" }, cancellationToken).ConfigureAwait(false);
            var reachable = TryGetBool(status, "coordinator", "reachable");
            var error = TryGetString(status, "coordinator", "error");
            var total = TryGetInt(status, "runners", "total");
            var (breakdown, slices) = BuildBreakdown(status);
            Dispatcher.Post(() =>
            {
                CoordinatorReachable = reachable;
                CoordinatorStateText = reachable == true
                    ? "reachable"
                    : error is not null ? $"not reachable ({error})" : "not reachable";
                RunnerCount = total;
                RunnerBreakdown = breakdown;
                RawStatusJson = Pretty(status);
                StatusBreakdown.Clear();
                foreach (var s in slices)
                    StatusBreakdown.Add(s);
            });
        }
        catch (TwCliException ex)
        {
            Dispatcher.Post(() =>
            {
                CoordinatorReachable = false;
                CoordinatorStateText = "offline";
                RawStatusJson = ex.Stderr;
                StatusBreakdown.Clear();
            });
        }
    }

    private static (string? Breakdown, IReadOnlyList<StatusSlice> Slices) BuildBreakdown(JsonElement status)
    {
        var byStatus = Navigate(status, "runners", "byStatus");
        if (byStatus is not { ValueKind: JsonValueKind.Object } el)
            return (null, Array.Empty<StatusSlice>());

        var parts = new List<string>();
        var slices = new List<StatusSlice>();
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var count) && count > 0)
            {
                parts.Add($"{count} {prop.Name}");
                var hex = CliPalette.StatusToHex(prop.Name) ?? CliPalette.NeutralHex;
                slices.Add(new StatusSlice(prop.Name, count, hex));
            }
        }
        return (parts.Count == 0 ? null : string.Join(", ", parts), slices);
    }

    private async Task LoadPingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ping = await _cli.RunOneShotAsync(new[] { "ping" }, cancellationToken).ConfigureAwait(false);
            var latency = TryGetDouble(ping, "latencyMs")
                ?? TryGetDouble(ping, "latency")
                ?? TryGetDouble(ping, "elapsed_ms")
                ?? TryGetDouble(ping, "elapsedMs");
            Dispatcher.Post(() =>
            {
                PingLatencyText = latency is { } l ? $"{l:N0} ms" : "ok";
            });
        }
        catch (TwCliException)
        {
            Dispatcher.Post(() => PingLatencyText = "unreachable");
        }
    }

    private async Task LoadInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var info = await _cli.RunOneShotAsync(new[] { "info" }, cancellationToken).ConfigureAwait(false);
            var plugins = ReadStringArray(info, "pluginRoots");
            var extensions = ReadStringArray(info, "extensions");
            Dispatcher.Post(() =>
            {
                ProductName = "Tinkwell";
                ProductVersion = TryGetString(info, "productVersion")
                    ?? TryGetString(info, "version");
                Architecture = TryGetString(info, "architecture");
                BaseDirectory = TryGetString(info, "baseDirectory");
                PluginRoots.Clear();
                foreach (var root in plugins)
                    PluginRoots.Add(root);
                Extensions.Clear();
                foreach (var ext in extensions)
                    Extensions.Add(ext);
                RawInfoJson = Pretty(info);
            });
        }
        catch (TwCliException ex)
        {
            Dispatcher.Post(() => RawInfoJson = ex.Stderr);
        }
    }

    /// <summary>
    /// Pulls <c>tw runners list</c> and <c>tw runners health --verbose</c> and
    /// projects them into <see cref="ResourceRow"/>s for the dashboard table.
    /// Rows are sorted by CPU descending so heavy runners float to the top.
    /// </summary>
    private async Task LoadResourceUsageAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> runners;
        IReadOnlyList<JsonElement> health;
        try
        {
            runners = await _cli.RunOneShotManyAsync(new[] { "runners", "list" }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            runners = Array.Empty<JsonElement>();
        }
        try
        {
            health = await _cli.RunOneShotManyAsync(new[] { "runners", "health" }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            health = Array.Empty<JsonElement>();
        }

        var healthByName = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in health)
        {
            var name = TryGetString(h, "runner") ?? TryGetString(h, "name");
            if (name is not null)
                healthByName[name] = h;
        }

        var healthyCount = 0;
        var totalReported = 0;
        var rows = new List<ResourceRow>();
        long maxMemory = 1;

        foreach (var runner in runners)
        {
            var name = TryGetString(runner, "name") ?? "(unnamed)";
            string? hStatus = null;
            double cpu = 0;
            string cpuText = "-";
            long memBytes = 0;
            string memText = "-";
            string? threads = null;

            if (healthByName.TryGetValue(name, out var h))
            {
                totalReported++;
                hStatus = TryGetString(h, "status");
                if (CliPalette.ClassifyStatus(hStatus) == DetailSemantic.Ok)
                    healthyCount++;

                var cpuRaw = NormalizeMissing(TryGetString(h, "cpuPercent"));
                if (cpuRaw is not null
                    && double.TryParse(cpuRaw.TrimEnd('%').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    cpu = parsed;
                    cpuText = $"{parsed:F1}%";
                }

                var memRaw = NormalizeMissing(TryGetString(h, "memory"));
                if (memRaw is not null)
                {
                    memBytes = ParseFormattedBytes(memRaw);
                    memText = memRaw;
                    if (memBytes > maxMemory)
                        maxMemory = memBytes;
                }

                threads = NormalizeMissing(TryGetString(h, "threads"));
            }

            rows.Add(new ResourceRow
            {
                Name = name,
                Status = hStatus,
                CpuPercent = cpu,
                CpuText = cpuText,
                MemoryBytes = memBytes,
                MemoryText = memText,
                Threads = threads,
            });
        }

        rows.Sort((a, b) => b.CpuPercent.CompareTo(a.CpuPercent));

        var effectiveMax = Math.Max(1, maxMemory);
        foreach (var row in rows)
            row.MaxMemoryBytes = effectiveMax;

        Dispatcher.Post(() =>
        {
            MaxMemoryBytes = effectiveMax;
            Resources.Clear();
            foreach (var row in rows)
                Resources.Add(row);
            HealthySummary = totalReported > 0
                ? $"{healthyCount} of {totalReported}"
                : RunnerCount is int n and > 0 ? $"- of {n}" : "-";
        });
    }

    /// <summary>
    /// Fires <c>services list</c>, <c>store list</c> and <c>measures list</c> in
    /// parallel and counts rows. Each call is wrapped so a single failure (e.g.
    /// store down) only blanks out its own card instead of the whole dashboard.
    /// </summary>
    private async Task LoadInventoryCountsAsync(CancellationToken cancellationToken)
    {
        var services = SafeCountAsync(new[] { "services", "list" }, cancellationToken);
        var store = SafeCountAsync(new[] { "store", "list" }, cancellationToken);
        var measures = SafeCountAsync(new[] { "measures", "list" }, cancellationToken);
        await Task.WhenAll(services, store, measures).ConfigureAwait(false);

        Dispatcher.Post(() =>
        {
            ServiceCount = services.Result;
            StoreCount = store.Result;
            MeasureCount = measures.Result;
        });
    }

    private async Task<int?> SafeCountAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _cli.RunOneShotManyAsync(args, cancellationToken).ConfigureAwait(false);
            return rows.Count;
        }
        catch
        {
            return null;
        }
    }

    private static long ParseFormattedBytes(string value)
    {
        var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return 0;
        if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return 0;
        var unit = (parts.Length > 1 ? parts[1] : "B").ToUpperInvariant();
        return unit switch
        {
            "GB" => (long)(n * 1_073_741_824.0),
            "MB" => (long)(n * 1_048_576.0),
            "KB" => (long)(n * 1_024.0),
            "B" => (long)n,
            _ => 0,
        };
    }

    private static string? NormalizeMissing(string? value)
        => string.IsNullOrEmpty(value) || value == "-" ? null : value;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            return System.Array.Empty<string>();

        var list = new List<string>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } s)
                list.Add(s);
        }
        return list;
    }

    private static bool? TryGetBool(JsonElement element, params string[] path)
    {
        var el = Navigate(element, path);
        if (el is null)
            return null;
        return el.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? TryGetString(JsonElement element, params string[] path)
        => Navigate(element, path) is { ValueKind: JsonValueKind.String } el ? el.GetString() : null;

    private static int? TryGetInt(JsonElement element, params string[] path)
    {
        if (Navigate(element, path) is not { } el)
            return null;
        return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i) ? i : null;
    }

    private static double? TryGetDouble(JsonElement element, params string[] path)
    {
        if (Navigate(element, path) is not { } el)
            return null;
        return el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d) ? d : null;
    }

    private static JsonElement? Navigate(JsonElement element, params string[] path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;
            if (!current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current;
    }

    private static string Pretty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
            return string.Empty;
        return JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
    }
}
