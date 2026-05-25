using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class StoreEntry : ObservableObject
{
    public StoreEntry(string key)
    {
        Key = key;
    }

    public string Key { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _bucketId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _namespace;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _createdAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _expiresAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// True once the full (un-truncated) value has been hydrated via
    /// <c>tw store get</c>. <c>tw store list</c> trims long blobs with "..." for
    /// the summary view; we lazy-load the real payload when a row is selected
    /// so both the details panel and the editor work with the actual content.
    /// </summary>
    [ObservableProperty]
    private bool _isFullValueLoaded;

    /// <summary>
    /// True while a <c>tw store get</c> call is in flight for this entry. The
    /// drawer binds a ProgressRing to this flag so the user can tell that the
    /// "..." they see in the value box is being upgraded to the full payload.
    /// </summary>
    [ObservableProperty]
    private bool _isHydrating;

    public ObservableCollection<string> ChangeLog { get; } = new();

    public IReadOnlyList<Detail> Details => new List<Detail>
    {
        new("Key", Key),
        new("Bucket", BucketId),
        new("Namespace", Namespace),
        new("Updated", UpdatedAt.ToLocalTime().ToString("u")),
        new("Created", CreatedAt),
        new("Expires", ExpiresAt),
    };
}

/// <summary>
/// One bucket bucket in the grouped store list. Mirrors
/// <c>MeasureGroup</c>: same <see cref="StoreEntry"/> instances live in
/// both the flat <see cref="StoreViewModel.Entries"/> collection and inside
/// one of these groups, so streaming property changes flow to both views.
/// </summary>
public sealed partial class StoreGroup : ObservableObject
{
    public StoreGroup(string bucketId)
    {
        BucketId = bucketId;
    }

    /// <summary>
    /// Bucket label as it will appear in the group header. Entries without a
    /// bucket id get filed under <see cref="StoreViewModel.DefaultBucketLabel"/>.
    /// </summary>
    public string BucketId { get; }

    public ObservableCollection<StoreEntry> Entries { get; } = new();
}

public sealed partial class StoreViewModel : CategoryViewModelBase
{
    /// <summary>
    /// Bucket label used for entries whose <c>bucketId</c> is blank or
    /// missing. Public so the view (and tests) can reference the same
    /// string.
    /// </summary>
    public const string DefaultBucketLabel = "Default";

    private readonly ITwCli _cli;
    private readonly Dictionary<StoreEntry, Task> _hydrations = new();
    // Master list, untouched by the filter. Streaming updates and hydration
    // lookups go through this so a hidden row still receives writes.
    private readonly List<StoreEntry> _allEntries = new();
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;

    public StoreViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Store";

    // Segoe Fluent Icons glyph: Database (EEA4).
    public override string Icon => "\uEEA4";

    public override bool SupportsSearch => true;

    public override string SearchPlaceholder => "Filter by bucket, namespace or key";

    /// <summary>
    /// Flat, filtered collection of entries. Kept alongside <see cref="Groups"/>
    /// so simple consumers (toolbar count, search lookups) don't have to walk
    /// the grouped tree.
    /// </summary>
    public ObservableCollection<StoreEntry> Entries { get; } = new();

    /// <summary>
    /// Filtered entries grouped by bucket id, ordered alphabetically with
    /// the synthetic <see cref="DefaultBucketLabel"/> bucket pushed to the
    /// end so named buckets appear first. The view renders one expandable
    /// section per group.
    /// </summary>
    public ObservableCollection<StoreGroup> Groups { get; } = new();

    [ObservableProperty]
    private StoreEntry? _selected;

    public bool IsDrawerOpen => Selected is not null;

    protected override void OnSearchTextUpdated(string value) => ApplyFilter();

    private bool MatchesFilter(StoreEntry entry)
    {
        var f = SearchText;
        if (string.IsNullOrWhiteSpace(f))
            return true;
        return Contains(entry.BucketId, f)
            || Contains(entry.Namespace, f)
            || Contains(entry.Key, f);
    }

    private static bool Contains(string? haystack, string needle)
        => haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void ApplyFilter()
    {
        Entries.Clear();
        Groups.Clear();

        // Build groups on the fly: a Dictionary keeps the per-bucket buckets
        // in insertion order, then we sort once at the end. Doing it this way
        // (instead of LINQ GroupBy + ordered selection) keeps the same
        // StoreEntry instances in both Entries and Groups so streaming
        // updates light up the rows in either view.
        var buckets = new Dictionary<string, StoreGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _allEntries)
        {
            if (!MatchesFilter(e))
                continue;

            Entries.Add(e);

            var key = BucketKeyFor(e);
            if (!buckets.TryGetValue(key, out var group))
            {
                group = new StoreGroup(key);
                buckets[key] = group;
            }
            group.Entries.Add(e);
        }

        // Show named buckets first (alphabetical, case-insensitive) and the
        // synthetic "Default" bucket last — uncategorized rows are a tail of
        // "everything else" by convention.
        foreach (var group in buckets.Values
            .OrderBy(g => string.Equals(g.BucketId, DefaultBucketLabel, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(g => g.BucketId, StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(group);
        }

        if (Selected is not null && !MatchesFilter(Selected))
            Selected = null;
    }

    /// <summary>
    /// Picks the group key for an entry: the bucket id if present, else the
    /// synthetic <see cref="DefaultBucketLabel"/>. Centralised so the
    /// streaming and refresh paths agree on what counts as "no bucket".
    /// </summary>
    private static string BucketKeyFor(StoreEntry entry)
        => string.IsNullOrWhiteSpace(entry.BucketId)
            ? DefaultBucketLabel
            : entry.BucketId!.Trim();

    /// <summary>
    /// Inserts a freshly-streamed entry into the matching group, creating
    /// the bucket on the fly when it's the first row for that bucket id.
    /// Caller is responsible for having already added the entry to
    /// <see cref="Entries"/> and <see cref="_allEntries"/>.
    /// </summary>
    private void AddToGroup(StoreEntry entry)
    {
        var key = BucketKeyFor(entry);
        var group = Groups.FirstOrDefault(g => string.Equals(g.BucketId, key, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            // Same ordering rule as ApplyFilter: Default last, everything
            // else alphabetical. Computing the insertion point here keeps
            // the visual order stable as new buckets appear.
            group = new StoreGroup(key);
            var isDefault = string.Equals(key, DefaultBucketLabel, StringComparison.OrdinalIgnoreCase);
            int insertAt = Groups.Count;
            for (int i = 0; i < Groups.Count; i++)
            {
                var current = Groups[i];
                var currentIsDefault = string.Equals(current.BucketId, DefaultBucketLabel, StringComparison.OrdinalIgnoreCase);
                if (isDefault)
                {
                    insertAt = Groups.Count;
                    break;
                }
                if (currentIsDefault
                    || string.Compare(key, current.BucketId, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertAt = i;
                    break;
                }
            }
            Groups.Insert(insertAt, group);
        }
        group.Entries.Insert(0, entry);
    }

    /// <summary>
    /// Removes an entry from whatever group currently contains it, dropping
    /// the group itself when it becomes empty so the UI doesn't leak empty
    /// expanders after a series of deletes.
    /// </summary>
    private void RemoveFromGroup(StoreEntry entry)
    {
        for (int i = 0; i < Groups.Count; i++)
        {
            var group = Groups[i];
            if (group.Entries.Remove(entry))
            {
                if (group.Entries.Count == 0)
                    Groups.RemoveAt(i);
                return;
            }
        }
    }

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string? _editorKey;

    [ObservableProperty]
    private string? _editorValue;

    [ObservableProperty]
    private string? _editorError;

    [ObservableProperty]
    private bool _editorIsNew;

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        await StartStreamAsync().ConfigureAwait(false);
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
            var many = await _cli.RunOneShotManyAsync(new[] { "store", "list" }, cancellationToken).ConfigureAwait(false);
            var rows = new List<StoreEntry>();
            foreach (var e in many)
            {
                var key = TryGetString(e, "key") ?? TryGetString(e, "name");
                if (key is null)
                    continue;
                var value = TryGetString(e, "value") ?? (e.TryGetProperty("value", out var v) ? v.ToString() : null);
                // Preserve the raw bucket / namespace strings from the CLI: they're
                // needed verbatim by `tw store get` to fetch the un-truncated value.
                rows.Add(new StoreEntry(key)
                {
                    Value = value,
                    BucketId = TryGetString(e, "bucketId") ?? TryGetString(e, "bucket"),
                    Namespace = TryGetString(e, "namespace") ?? TryGetString(e, "keyNamespace"),
                    CreatedAt = NormalizeDash(TryGetString(e, "createdAt") ?? TryGetString(e, "created")),
                    ExpiresAt = NormalizeDash(TryGetString(e, "expiresAt") ?? TryGetString(e, "expires")),
                });
            }
            Dispatcher.Post(() =>
            {
                _allEntries.Clear();
                _allEntries.AddRange(rows);
                _hydrations.Clear();
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
    private void OpenNew()
    {
        EditorKey = string.Empty;
        EditorValue = string.Empty;
        EditorError = null;
        EditorIsNew = true;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task OpenEditAsync()
    {
        if (Selected is null)
            return;

        var entry = Selected;
        await EnsureFullValueAsync(entry, CancellationToken.None).ConfigureAwait(false);

        Dispatcher.Post(() =>
        {
            // Re-check the user's selection: if they navigated away while we were
            // fetching, don't pop the editor for the wrong row.
            if (Selected != entry)
                return;
            EditorKey = entry.Key;
            EditorValue = entry.Value;
            EditorError = null;
            EditorIsNew = false;
            IsEditorOpen = true;
        });
    }

    /// <summary>
    /// Hydrates an entry with the un-truncated value via <c>tw store get</c> when
    /// it's still showing the summary blob from <c>tw store list</c>. Hydration
    /// runs at most once per entry and is shared across concurrent callers
    /// (selection + edit) via a per-entry task cache.
    /// </summary>
    private Task EnsureFullValueAsync(StoreEntry entry, CancellationToken cancellationToken)
    {
        if (entry.IsFullValueLoaded)
            return Task.CompletedTask;

        // Cache the in-flight task on the entry so a fast click sequence
        // (select → edit) reuses the single fetch.
        if (_hydrations.TryGetValue(entry, out var pending))
            return pending;

        var task = HydrateAsync(entry, cancellationToken);
        _hydrations[entry] = task;
        return task;
    }

    private async Task HydrateAsync(StoreEntry entry, CancellationToken cancellationToken)
    {
        Dispatcher.Post(() => entry.IsHydrating = true);
        try
        {
            var full = await TryFetchFullValueAsync(entry, cancellationToken).ConfigureAwait(false);
            if (full is null)
                return;
            Dispatcher.Post(() =>
            {
                entry.Value = full;
                entry.IsFullValueLoaded = true;
            });
        }
        finally
        {
            Dispatcher.Post(() => entry.IsHydrating = false);
            _hydrations.Remove(entry);
        }
    }

    /// <summary>
    /// Calls <c>tw store get &lt;key&gt; -b &lt;bucketId&gt; -s &lt;namespace&gt;</c> and returns the
    /// <c>value</c> field. Returns <c>null</c> if the entry has no bucket id (we can't
    /// look it up in that case) or if the CLI call fails.
    /// </summary>
    private async Task<string?> TryFetchFullValueAsync(StoreEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entry.BucketId))
            return null;

        var args = new List<string> { "store", "get", entry.Key, "-b", entry.BucketId };
        if (entry.Namespace is not null)
        {
            args.Add("-s");
            args.Add(entry.Namespace);
        }

        try
        {
            var element = await _cli.RunOneShotAsync(args, cancellationToken).ConfigureAwait(false);
            if (element.ValueKind != JsonValueKind.Object)
                return null;
            return TryGetString(element, "value");
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch
        {
            // Soft-fail: keep the truncated summary if the lookup fails. The drawer
            // still renders, and we'll retry the next time the row is selected.
            return null;
        }
    }

    [RelayCommand]
    private void CloseEditor() => IsEditorOpen = false;

    [RelayCommand]
    private async Task SubmitEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorKey))
        {
            EditorError = "Key is required.";
            return;
        }
        try
        {
            await _cli.RunOneShotAsync(new[] { "store", "set", EditorKey!, EditorValue ?? string.Empty }, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                IsEditorOpen = false;
                StatusMessage = $"Wrote `{EditorKey}`.";
            });
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => EditorError = ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null)
            return;
        try
        {
            await _cli.RunOneShotAsync(new[] { "store", "delete", Selected.Key }, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.Post(() =>
            {
                var target = Selected;
                if (target is null)
                    return;
                _allEntries.Remove(target);
                Entries.Remove(target);
                RemoveFromGroup(target);
                Selected = null;
            });
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    [RelayCommand]
    private void CloseDrawer() => Selected = null;

    partial void OnSelectedChanged(StoreEntry? value)
    {
        OnPropertyChanged(nameof(IsDrawerOpen));
        // Kick off a fire-and-forget hydration so the drawer's read-only value
        // box upgrades from the truncated summary to the full payload without
        // the user having to click Edit. The same cached task will be reused
        // if Edit is clicked before the fetch completes.
        if (value is not null && !value.IsFullValueLoaded)
            _ = EnsureFullValueAsync(value, CancellationToken.None);
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
        try
        {
            await foreach (var element in _cli.StreamAsync(new[] { "store", "watch" }, cancellationToken).ConfigureAwait(false))
            {
                var key = TryGetString(element, "key") ?? TryGetString(element, "name");
                if (key is null)
                    continue;
                var value = TryGetString(element, "value") ?? (element.TryGetProperty("value", out var v) ? v.ToString() : null);
                var operation = TryGetString(element, "op") ?? TryGetString(element, "operation") ?? "update";

                Dispatcher.Post(() =>
                {
                    // Lookups go against the master list so updates still apply
                    // when the active filter is hiding the entry from the UI.
                    var existing = _allEntries.FirstOrDefault(x => x.Key == key);
                    if (operation.Equals("delete", StringComparison.OrdinalIgnoreCase))
                    {
                        if (existing is not null)
                        {
                            _allEntries.Remove(existing);
                            Entries.Remove(existing);
                            RemoveFromGroup(existing);
                        }
                        return;
                    }
                    if (existing is null)
                    {
                        existing = new StoreEntry(key);
                        _allEntries.Insert(0, existing);
                        if (MatchesFilter(existing))
                        {
                            Entries.Insert(0, existing);
                            AddToGroup(existing);
                        }
                    }
                    existing.Value = value;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    existing.ChangeLog.Insert(0, $"{DateTime.Now:HH:mm:ss} {operation}: {value}");
                    while (existing.ChangeLog.Count > 200)
                        existing.ChangeLog.RemoveAt(existing.ChangeLog.Count - 1);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            Dispatcher.Post(() => SetError(ex));
        }
    }

    private static string? NormalizeDash(string? value)
        => string.IsNullOrEmpty(value) || value == "-" ? null : value;

    private static string? TryGetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
