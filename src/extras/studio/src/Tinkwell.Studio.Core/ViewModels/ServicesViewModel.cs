using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class ServiceRow : ObservableObject
{
    public ServiceRow(
        string name,
        string? type,
        string? friendlyName,
        string? familyName,
        string? aliases,
        string? host,
        string? url,
        JsonElement raw)
    {
        Name = name;
        Type = type;
        FriendlyName = friendlyName;
        FamilyName = familyName;
        Aliases = aliases;
        Host = host;
        Url = url;
        Raw = raw;
    }

    public string Name { get; }

    public string? Type { get; }

    public string? FriendlyName { get; }

    public string? FamilyName { get; }

    public string? Aliases { get; }

    public string? Host { get; }

    public string? Url { get; }

    public JsonElement Raw { get; }

    public IReadOnlyList<Detail> Details
    {
        get
        {
            var list = new List<Detail>
            {
                new("Friendly Name", FriendlyName),
                new("Name", Name),
                new("Type", Type),
                new("Family name", FamilyName),
                new("Aliases", string.IsNullOrEmpty(Aliases) ? "(none)" : Aliases),
                new("Host", Host, DetailKind.Url),
                new("URL", Url, DetailKind.Url),
            };
            foreach (var detail in DetailsBuilder.FromElement(Raw, _skipAlreadyShown))
                list.Add(detail);
            return list;
        }
    }

    private static readonly HashSet<string> _skipAlreadyShown = new(StringComparer.Ordinal)
    {
        "name", "type", "friendlyName", "familyName", "aliases", "host", "url"
    };
}

public sealed partial class ServicesViewModel : CategoryViewModelBase
{
    private readonly ITwCli _cli;
    // Master snapshot from the most recent refresh; the visible collection
    // is rebuilt from it whenever the filter text changes.
    private readonly List<ServiceRow> _allServices = new();

    public ServicesViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Services";

    // Segoe Fluent Icons glyph: NetworkTower (EC05).
    public override string Icon => "\uEC05";

    public override bool SupportsSearch => true;

    public override string SearchPlaceholder => "Filter by family, name or friendly name";

    public ObservableCollection<ServiceRow> Services { get; } = new();

    [ObservableProperty]
    private ServiceRow? _selected;

    public bool IsDrawerOpen => Selected is not null;

    public string? SelectedRawJson => Selected is null
        ? null
        : JsonSerializer.Serialize(Selected.Raw, new JsonSerializerOptions { WriteIndented = true });

    protected override void OnSearchTextUpdated(string value) => ApplyFilter();

    private bool MatchesFilter(ServiceRow row)
    {
        var f = SearchText;
        if (string.IsNullOrWhiteSpace(f))
            return true;
        return Contains(row.FamilyName, f)
            || Contains(row.Name, f)
            || Contains(row.FriendlyName, f);
    }

    private static bool Contains(string? haystack, string needle)
        => haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void ApplyFilter()
    {
        Services.Clear();
        foreach (var s in _allServices)
        {
            if (MatchesFilter(s))
                Services.Add(s);
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
            var many = await _cli.RunOneShotManyAsync(new[] { "services", "list" }, cancellationToken).ConfigureAwait(false);
            var rows = many.Select(CreateRow).ToList();
            Dispatcher.Post(() =>
            {
                _allServices.Clear();
                _allServices.AddRange(rows);
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

    partial void OnSelectedChanged(ServiceRow? value)
    {
        OnPropertyChanged(nameof(IsDrawerOpen));
        OnPropertyChanged(nameof(SelectedRawJson));
    }

    private static ServiceRow CreateRow(JsonElement element)
    {
        var name = TryGetString(element, "name") ?? "(unnamed)";
        var type = TryGetString(element, "type");
        var friendlyName = TryGetString(element, "friendlyName");
        var familyName = TryGetString(element, "familyName");
        var aliases = ParseAliases(element);
        var host = TryGetString(element, "host");
        var url = TryGetString(element, "url");
        return new ServiceRow(name, type, friendlyName, familyName, aliases, host, url, element);
    }

    private static string? ParseAliases(JsonElement element)
    {
        if (!element.TryGetProperty("aliases", out var el))
            return null;

        if (el.ValueKind == JsonValueKind.String)
            return el.GetString();

        if (el.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<string>();
        foreach (var a in el.EnumerateArray())
        {
            if (a.ValueKind == JsonValueKind.String && a.GetString() is { } s && s.Length > 0)
                list.Add(s);
        }
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v))
            return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
