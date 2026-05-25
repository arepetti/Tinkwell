using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

/// <summary>
/// High-level classification of a measure, derived from the runtime's
/// <c>MeasureAttributes</c> flags. The CLI exposes the flags as a
/// comma-separated string (e.g. <c>"None"</c>, <c>"Constant"</c>,
/// <c>"Derived, System"</c>); the view models map that to one of these
/// buckets so the UI can render a per-row indicator.
/// </summary>
public enum MeasureKind
{
    /// <summary>Plain measure: free-running value, no expression, no system origin.</summary>
    Normal,

    /// <summary>Measure flagged as <c>Constant</c>: value is set once and pinned.</summary>
    Constant,

    /// <summary>Measure flagged as <c>Derived</c>: value comes from an expression
    /// over other measures.</summary>
    Calculated,

    /// <summary>Measure flagged as <c>System</c>: created internally by the runtime.</summary>
    System,
}

public sealed partial class MeasureEntry : ObservableObject
{
    public MeasureEntry(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _unit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(ValueNumeric))]
    [NotifyPropertyChangedFor(nameof(HasRangeBar))]
    private string? _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _category;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(MinNumeric))]
    [NotifyPropertyChangedFor(nameof(HasRangeBar))]
    private string? _min;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(MaxNumeric))]
    [NotifyPropertyChangedFor(nameof(HasRangeBar))]
    private string? _max;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _ttl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _precision;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _tags;

    /// <summary>
    /// Raw <c>attributes</c> string from <c>tw measures list</c>, e.g.
    /// <c>"None"</c>, <c>"Constant"</c>, <c>"Derived, System"</c>. Kept around
    /// so the detail drawer can show the original flag combination; the
    /// table-row indicator uses <see cref="Kind"/> instead.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kind))]
    [NotifyPropertyChangedFor(nameof(Tone))]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _attributes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _oldValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    public ObservableCollection<string> ChangeLog { get; } = new();

    /// <summary>
    /// Parsed numeric <see cref="Value"/> for the inline progress bar.
    /// Falls back to <see cref="MinNumeric"/> when the value is missing
    /// or non-numeric, so the bar shows "empty" rather than throwing on
    /// <see cref="double.NaN"/> (which the WinUI <c>ProgressBar</c> does
    /// not accept).
    /// </summary>
    public double ValueNumeric
        => TryParseNumber(Value, out var v) ? v : MinNumeric;

    /// <summary>Parsed numeric <see cref="Min"/>, defaulting to <c>0</c> when missing.</summary>
    public double MinNumeric
        => TryParseNumber(Min, out var v) ? v : 0.0;

    /// <summary>
    /// Parsed numeric <see cref="Max"/>. Defaults to <c>1</c> (instead of
    /// <c>0</c>) when missing so a <c>ProgressBar</c> bound to it doesn't
    /// throw on a zero-width range; the bar is hidden via
    /// <see cref="HasRangeBar"/> in that case anyway.
    /// </summary>
    public double MaxNumeric
        => TryParseNumber(Max, out var v) ? v : 1.0;

    /// <summary>
    /// True when <see cref="Min"/>, <see cref="Max"/>, and <see cref="Value"/>
    /// are all numeric and <c>Max &gt; Min</c>. Drives the inline
    /// progress bar's visibility: measures without configured bounds
    /// (or with non-numeric values) just show the text.
    /// </summary>
    public bool HasRangeBar
        => TryParseNumber(Min, out var lo)
            && TryParseNumber(Max, out var hi)
            && hi > lo
            && TryParseNumber(Value, out _);

    /// <summary>
    /// Best-effort parse of a measure value into a <see cref="double"/>.
    /// Accepts plain numerics ("21.3"), invariant-formatted floats and the
    /// leading numeric token of "value unit" strings ("21.3 °C"), so the
    /// range-bar binding still works for measures whose CLI representation
    /// includes the unit. Exposed at <c>internal</c> so the streaming
    /// consumer can reuse it for numeric-equivalence dedupe.
    /// </summary>
    internal static bool TryParseNumber(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return true;

        // Strip a trailing unit (e.g. "21.3 °C") and retry.
        var span = value.AsSpan().TrimStart();
        int end = 0;
        while (end < span.Length
            && (char.IsDigit(span[end]) || span[end] == '.' || span[end] == '-' || span[end] == '+' || span[end] == 'e' || span[end] == 'E'))
        {
            end++;
        }
        if (end == 0)
            return false;
        return double.TryParse(span.Slice(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Bucketed <see cref="MeasureKind"/> for the row indicator. Constant wins
    /// over Derived (a measure with both flags is conceptually a constant
    /// expression, but the user-facing distinction is "this never changes").
    /// </summary>
    public MeasureKind Kind => ClassifyAttributes(Attributes);

    /// <summary>
    /// <see cref="IndicatorTone"/> for the row chip. Mirrors <see cref="Kind"/>:
    /// constant measures use the muted ("locked") tone, derived measures get
    /// the accent tone (they're computed from other measures, conceptually
    /// "special"), system measures fall back to muted, and plain measures use
    /// the neutral default tone.
    /// </summary>
    public IndicatorTone Tone => Kind switch
    {
        MeasureKind.Constant => IndicatorTone.Muted,
        MeasureKind.Calculated => IndicatorTone.Accent,
        MeasureKind.System => IndicatorTone.Muted,
        _ => IndicatorTone.Default,
    };

    public IReadOnlyList<Detail> Details => new List<Detail>
    {
        new("Name", Name),
        new("Value", Value),
        new("Unit", Unit),
        new("Category", Category),
        new("Type", Type),
        new("Minimum", Min),
        new("Maximum", Max),
        new("TTL (s)", Ttl),
        new("Precision", Precision),
        new("Attributes", Attributes),
        new("Tags", Tags),
        new("Previous value", OldValue),
        new("Updated", UpdatedAt.ToLocalTime().ToString("u")),
    };

    internal static MeasureKind ClassifyAttributes(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes) || attributes == "-")
            return MeasureKind.Normal;
        // The CLI emits the [Flags] enum's ToString form, e.g. "Constant",
        // "Derived", "System" or "Constant, System". Match by token so combined
        // values still classify, and so we don't depend on flag ordering.
        var tokens = attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool hasConstant = false, hasDerived = false, hasSystem = false;
        foreach (var token in tokens)
        {
            if (token.Equals("Constant", StringComparison.OrdinalIgnoreCase))
                hasConstant = true;
            else if (token.Equals("Derived", StringComparison.OrdinalIgnoreCase))
                hasDerived = true;
            else if (token.Equals("System", StringComparison.OrdinalIgnoreCase))
                hasSystem = true;
        }

        if (hasConstant)
            return MeasureKind.Constant;
        if (hasDerived)
            return MeasureKind.Calculated;
        if (hasSystem)
            return MeasureKind.System;
        return MeasureKind.Normal;
    }
}

/// <summary>
/// One category bucket in the grouped measures list. Acts purely as a
/// visual container: the same <see cref="MeasureEntry"/> instances live in
/// <see cref="MeasuresViewModel.Measures"/> (the flat, filtered collection)
/// and inside one of these groups, so streaming property changes flow to
/// both views without extra plumbing.
/// </summary>
public sealed partial class MeasureGroup : ObservableObject
{
    public MeasureGroup(string category)
    {
        Category = category;
    }

    /// <summary>
    /// Category label as it will appear in the group header. Measures
    /// without a category get bucketed under <see cref="MeasuresViewModel.DefaultCategoryLabel"/>.
    /// </summary>
    public string Category { get; }

    public ObservableCollection<MeasureEntry> Measures { get; } = new();
}

public sealed partial class MeasuresViewModel : CategoryViewModelBase
{
    /// <summary>
    /// Bucket label used for measures whose <c>category</c> property is
    /// blank or missing. Kept as a public constant so the view (and tests)
    /// can reference the same string.
    /// </summary>
    public const string DefaultCategoryLabel = "Default";

    private readonly ITwCli _cli;
    // Source-of-truth list, kept independent of the visible (filtered) collection
    // so the streaming consumer can still find existing entries even when the
    // filter is hiding them from the UI.
    private readonly List<MeasureEntry> _allMeasures = new();
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;

    public MeasuresViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Measures";

    // Segoe Fluent Icons glyph: DataSense (E790).
    public override string Icon => "\uE9D9";

    public override bool SupportsSearch => true;

    public override string SearchPlaceholder => "Filter by name";

    /// <summary>
    /// Flat, filtered collection of measures. Kept alongside
    /// <see cref="Groups"/> so simple consumers (toolbar count, search/edit
    /// look-ups) don't have to walk the grouped tree.
    /// </summary>
    public ObservableCollection<MeasureEntry> Measures { get; } = new();

    /// <summary>
    /// Filtered collection grouped by category, ordered alphabetically with
    /// the <see cref="DefaultCategoryLabel"/> bucket pushed to the end so
    /// categorized measures appear first. The view renders one expandable
    /// section per group.
    /// </summary>
    public ObservableCollection<MeasureGroup> Groups { get; } = new();

    [ObservableProperty]
    private MeasureEntry? _selected;

    public bool IsDrawerOpen => Selected is not null;

    [ObservableProperty]
    private bool _isSetOpen;

    [ObservableProperty]
    private string? _setValue;

    [ObservableProperty]
    private string? _setError;

    protected override void OnSearchTextUpdated(string value) => ApplyFilter();

    private bool MatchesFilter(MeasureEntry entry)
    {
        var f = SearchText;
        if (string.IsNullOrWhiteSpace(f))
            return true;
        return entry.Name.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        Measures.Clear();
        Groups.Clear();

        // Build groups on the fly: a Dictionary keeps the per-category
        // buckets in insertion order, then we sort once at the end. Doing
        // it this way (instead of LINQ GroupBy + ordered selection) keeps
        // the same MeasureEntry instances in both Measures and Groups so
        // streaming updates light up the rows in either view.
        var buckets = new Dictionary<string, MeasureGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _allMeasures)
        {
            if (!MatchesFilter(m))
                continue;

            Measures.Add(m);

            var key = CategoryKeyFor(m);
            if (!buckets.TryGetValue(key, out var group))
            {
                group = new MeasureGroup(key);
                buckets[key] = group;
            }
            group.Measures.Add(m);
        }

        // Show categorized buckets first (alphabetical, case-insensitive)
        // and the synthetic "Default" bucket last — uncategorized rows are
        // a tail of "everything else" by convention.
        foreach (var group in buckets.Values
            .OrderBy(g => string.Equals(g.Category, DefaultCategoryLabel, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(g => g.Category, StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(group);
        }

        // Drop the selection if the user just hid the row it points to;
        // the drawer stays open otherwise but no row is highlighted, which
        // looks like a UI bug.
        if (Selected is not null && !MatchesFilter(Selected))
            Selected = null;
    }

    /// <summary>
    /// Picks the group key for a measure: the category if present, else
    /// the synthetic <see cref="DefaultCategoryLabel"/> bucket. Centralised
    /// so the streaming and refresh paths agree on what counts as
    /// "uncategorized".
    /// </summary>
    private static string CategoryKeyFor(MeasureEntry entry)
        => string.IsNullOrWhiteSpace(entry.Category)
            ? DefaultCategoryLabel
            : entry.Category!.Trim();

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        // Start the watch first so any value change emitted while the
        // refresh's gRPC roundtrip is in flight is captured. The consumer
        // upserts entries by name, so the snapshot loaded by RefreshAsync
        // and the streamed updates merge cleanly regardless of which side
        // wins the race.
        await StartStreamAsync().ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task OnDeactivatedAsync()
        => await StopStreamAsync().ConfigureAwait(false);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var many = await _cli.RunOneShotManyAsync(new[] { "measures", "list" }, cancellationToken).ConfigureAwait(false);
            var rows = new List<MeasureEntry>();
            foreach (var e in many)
            {
                var name = TryGetString(e, "name");
                if (name is null)
                    continue;
                rows.Add(new MeasureEntry(name)
                {
                    Unit = NormalizeDash(TryGetString(e, "unit")),
                    // The CLI prints "-" for undefined values; normalize so the
                    // table cell renders blank and the Edit dialog doesn't
                    // pre-fill the textbox with a literal dash.
                    Value = NormalizeDash(TryGetStringAny(e, "value")),
                    Category = NormalizeDash(TryGetString(e, "category")),
                    Type = NormalizeDash(TryGetString(e, "type")),
                    Min = NormalizeDash(TryGetStringAny(e, "min") ?? TryGetStringAny(e, "minimum")),
                    Max = NormalizeDash(TryGetStringAny(e, "max") ?? TryGetStringAny(e, "maximum")),
                    Ttl = NormalizeDash(TryGetStringAny(e, "ttl") ?? TryGetStringAny(e, "ttlSeconds")),
                    Precision = NormalizeDash(TryGetStringAny(e, "precision")),
                    Attributes = NormalizeDash(TryGetString(e, "attributes")),
                    Tags = NormalizeDash(TryGetString(e, "tags")),
                });
            }
            Dispatcher.Post(() =>
            {
                _allMeasures.Clear();
                _allMeasures.AddRange(rows);
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

    /// <summary>
    /// Whether the currently selected measure can be set to a new value.
    /// Constants and derived/calculated measures are read-only by design
    /// (the runtime rejects writes), so we hide the affordance up front
    /// instead of letting the user fire a request that the coordinator
    /// will refuse. Drives <c>OpenSetCommand.CanExecute</c>.
    /// </summary>
    public bool CanEditSelected
        => Selected is not null
            && Selected.Kind != MeasureKind.Constant
            && Selected.Kind != MeasureKind.Calculated;

    [RelayCommand(CanExecute = nameof(CanEditSelected))]
    private void OpenSet()
    {
        if (Selected is null)
            return;
        SetValue = Selected.Value;
        SetError = null;
        IsSetOpen = true;
    }

    [RelayCommand]
    private void CloseSet() => IsSetOpen = false;

    [RelayCommand]
    private async Task SubmitSetAsync()
    {
        if (Selected is null)
            return;
        if (string.IsNullOrWhiteSpace(SetValue))
        {
            SetError = "Value is required.";
            return;
        }
        var target = Selected;
        var newValue = SetValue!;
        try
        {
            await _cli.RunOneShotAsync(
                new[] { "measures", "set", target.Name, newValue },
                CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                // Apply the new value optimistically: the gRPC watch will
                // re-confirm it shortly, but waiting for that round-trip
                // makes the UI feel like the edit was lost (the row keeps
                // the old value for a beat after the dialog closes). The
                // watch event is idempotent — same name, same value — so
                // overlapping with this assignment is harmless.
                target.OldValue = target.Value;
                target.Value = newValue;
                target.UpdatedAt = DateTimeOffset.UtcNow;
                // Record the edit in the change log here: the watch
                // confirmation will be filtered out by the equivalence
                // check in ConsumeOnceAsync (same value), so without this
                // line a user-initiated set would never appear in the
                // measure's history.
                AppendChangeLog(target, newValue, target.Unit);
                IsSetOpen = false;
                StatusMessage = $"Set `{target.Name}`.";
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError = ex.Message);
        }
    }

    [RelayCommand]
    private void CloseDrawer() => Selected = null;

    partial void OnSelectedChanged(MeasureEntry? value)
    {
        OnPropertyChanged(nameof(IsDrawerOpen));
        OnPropertyChanged(nameof(CanEditSelected));
        // Refresh the Edit button's IsEnabled when the selection changes
        // (and therefore the read-only-ness of the row potentially flips).
        OpenSetCommand.NotifyCanExecuteChanged();
    }

    private async Task StartStreamAsync()
    {
        await StopStreamAsync().ConfigureAwait(false);
        _streamCts = new CancellationTokenSource();
        _streamTask = Task.Run(() => ConsumeAsync(_streamCts.Token));
    }

    private async Task StopStreamAsync()
    {
        _streamCts?.Cancel();
        if (_streamTask is not null)
        {
            try
            {
                await _streamTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }
        _streamCts?.Dispose();
        _streamCts = null;
        _streamTask = null;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        // The watch stream can drop (the coordinator restarts, the gRPC channel
        // hiccups, the tw process exits cleanly when stdin closes, ...). When
        // that happens the table would silently stop receiving updates; the
        // user sees stale values and assumes "the watch is broken". Wrap the
        // consume loop in a retry-with-backoff harness so the stream is
        // re-established transparently as long as the category is active.
        var backoff = TimeSpan.FromMilliseconds(500);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeOnceAsync(cancellationToken).ConfigureAwait(false);
                // Stream ended cleanly: reset backoff and reconnect promptly.
                backoff = TimeSpan.FromMilliseconds(500);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                Dispatcher.Post(() => SetError(ex));
                backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, 10_000));
            }

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
        await foreach (var element in _cli.StreamAsync(new[] { "measures", "watch" }, cancellationToken).ConfigureAwait(false))
        {
            var name = TryGetString(element, "name");
            if (name is null)
                continue;
            // The watch CLI emits "<undefined>" for measures with no value;
            // map both that and the list command's "-" to null so the UI
            // renders a blank cell.
            var value = NormalizeUndefined(TryGetStringAny(element, "value"));
            var unit = TryGetString(element, "unit");
            var type = TryGetString(element, "type");
            var oldValue = NormalizeUndefined(TryGetStringAny(element, "oldValue"));

            Dispatcher.Post(() =>
            {
                // Look up in the master list so streaming updates still reach
                // entries that the active filter is hiding from the UI.
                var existing = _allMeasures.FirstOrDefault(x => x.Name == name);
                if (existing is null)
                {
                    // The watch event doesn't carry category/min/max/etc.;
                    // those will fill in on the next refresh. Until then
                    // the new row sits in the Default bucket.
                    existing = new MeasureEntry(name) { Unit = unit, Value = value, Type = type, OldValue = oldValue };
                    _allMeasures.Add(existing);
                    if (MatchesFilter(existing))
                    {
                        Measures.Add(existing);
                        AddToGroup(existing);
                    }
                }
                else
                {
                    // The coordinator's measures watch fans the same store
                    // event out through every internal subscriber loop
                    // (signals, actions, event-bus, ...), so a single
                    // user-issued `set` typically lands on Studio as
                    // several events with the same payload — sometimes
                    // even with slightly different formatting ("12" vs
                    // "12.0") because each producer renders the number
                    // independently. Compare numerically when both sides
                    // parse, fall back to string equality otherwise, so
                    // those re-broadcasts don't pollute the ChangeLog or
                    // bump UpdatedAt every tick.
                    var changed = !ValuesAreEquivalent(existing.Value, value);
                    if (changed)
                    {
                        existing.OldValue = existing.Value;
                        existing.Value = value;
                        existing.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    if (unit is not null)
                        existing.Unit = unit;
                    if (type is not null)
                        existing.Type = type;
                    if (oldValue is not null)
                        existing.OldValue = oldValue;
                    if (!changed)
                        return;
                }
                AppendChangeLog(existing, value, unit ?? existing.Unit);
            });
        }
    }

    /// <summary>
    /// Inserts a freshly-streamed entry into the matching group, creating
    /// the bucket on the fly when it's the first measure for that
    /// category. Caller is responsible for having already added the entry
    /// to <see cref="Measures"/> and <see cref="_allMeasures"/>.
    /// </summary>
    private void AddToGroup(MeasureEntry entry)
    {
        var key = CategoryKeyFor(entry);
        var group = Groups.FirstOrDefault(g => string.Equals(g.Category, key, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            // Same ordering rule as ApplyFilter: Default goes last,
            // everything else alphabetical. Computing the insertion point
            // here keeps the visual order stable as new categories appear.
            group = new MeasureGroup(key);
            var isDefault = string.Equals(key, DefaultCategoryLabel, StringComparison.OrdinalIgnoreCase);
            int insertAt = Groups.Count;
            for (int i = 0; i < Groups.Count; i++)
            {
                var current = Groups[i];
                var currentIsDefault = string.Equals(current.Category, DefaultCategoryLabel, StringComparison.OrdinalIgnoreCase);
                if (isDefault)
                {
                    // We always sit at the very end.
                    insertAt = Groups.Count;
                    break;
                }
                if (currentIsDefault
                    || string.Compare(key, current.Category, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertAt = i;
                    break;
                }
            }
            Groups.Insert(insertAt, group);
        }
        group.Measures.Add(entry);
    }

    /// <summary>
    /// Appends a single line to the measure's change log, capped at the
    /// last 200 entries (oldest dropped). Centralised so the streaming
    /// path and the optimistic-edit path produce identical formatting.
    /// Suppresses immediate duplicates: a single user-issued <c>set</c>
    /// can land on both <see cref="SubmitSetAsync"/> (optimistic) and
    /// <see cref="ConsumeOnceAsync"/> (watch confirmation) within a
    /// handful of milliseconds — and depending on which one wins the
    /// race, the watch dedupe may not catch the second log call. The
    /// real duplicate-watch scenario (same value, twice in a row) is
    /// already filtered upstream by <see cref="ValuesAreEquivalent"/>,
    /// so suppressing same-value-as-most-recent here doesn't lose any
    /// legitimate entries. Caller must be on the UI thread.
    /// </summary>
    private static void AppendChangeLog(MeasureEntry entry, string? value, string? unit)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {value} {unit ?? string.Empty}".TrimEnd();
        if (entry.ChangeLog.Count > 0
            && ChangeLogValuesMatch(entry.ChangeLog[0], line))
            return;
        entry.ChangeLog.Insert(0, line);
        while (entry.ChangeLog.Count > 200)
            entry.ChangeLog.RemoveAt(entry.ChangeLog.Count - 1);
    }

    /// <summary>
    /// True when two formatted change-log lines record the same value.
    /// The format is <c>"HH:mm:ss VAL UNIT"</c>; we strip the timestamp
    /// (8 chars + a space when present) and compare the payload.
    /// </summary>
    private static bool ChangeLogValuesMatch(string a, string b)
    {
        return string.Equals(StripTimestamp(a), StripTimestamp(b), StringComparison.Ordinal);

        static string StripTimestamp(string s)
            => s.Length > 9 && s[2] == ':' && s[5] == ':' && s[8] == ' '
                ? s.AsSpan(9).ToString()
                : s;
    }

    /// <summary>
    /// True when two value strings represent the same payload. Numerics
    /// compare on the parsed double (so "12" and "12.0" tie), strings fall
    /// back to ordinal equality with both sides null treated as equal.
    /// </summary>
    private static bool ValuesAreEquivalent(string? a, string? b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;
        if (MeasureEntry.TryParseNumber(a, out var da)
            && MeasureEntry.TryParseNumber(b, out var db))
            return da.Equals(db);
        return false;
    }

    private static string? NormalizeDash(string? value)
        => string.IsNullOrEmpty(value) || value == "-" ? null : value;

    /// <summary>
    /// The watch CLI prints <c>"&lt;undefined&gt;"</c> for measures with no
    /// value and the list CLI prints <c>"-"</c>; this helper collapses both
    /// to <c>null</c> so the UI doesn't surface either placeholder.
    /// </summary>
    private static string? NormalizeUndefined(string? value)
        => string.IsNullOrEmpty(value) || value == "-" || value == "<undefined>" ? null : value;

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? TryGetStringAny(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v))
            return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => v.ToString(),
        };
    }
}
