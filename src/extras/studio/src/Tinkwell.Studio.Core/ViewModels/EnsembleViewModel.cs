using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Configuration.Parser;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

/// <summary>
/// A flat key/value pair shown in the Ensemble detail drawer.
/// Used for both block modifiers and block properties.
/// </summary>
public sealed record EnsembleDetailItem(string Key, string Value);

/// <summary>
/// A single block in the ensemble tree (runner, measure, binding, ...).
/// </summary>
/// <remarks>
/// The node is parser-agnostic: it only exposes the block's type, a display
/// title, its modifiers and properties as flat lists, and its children. The
/// accent color is kept as a hex string here to preserve UI-framework neutrality;
/// the view layer converts it to the appropriate brush type.
/// </remarks>
public sealed partial class EnsembleBlockNode : ObservableObject
{
    public EnsembleBlockNode(
        string type,
        string title,
        IReadOnlyList<EnsembleDetailItem> modifiers,
        IReadOnlyList<EnsembleDetailItem> properties,
        IReadOnlyList<EnsembleBlockNode> children,
        string accentColorHex,
        int depth,
        bool isExpanded,
        IRelayCommand<EnsembleBlockNode> selectCommand)
    {
        Type = type;
        Title = title;
        Modifiers = modifiers;
        Properties = properties;
        Children = children;
        AccentColorHex = accentColorHex;
        Depth = depth;
        _isExpanded = isExpanded;
        SelectBlockCommand = selectCommand;
    }

    /// <summary>
    /// Shared command (one instance, owned by the parent <see cref="EnsembleViewModel"/>)
    /// that opens the detail drawer for this node. Carried on the node so data
    /// templates can bind to <c>{Binding SelectBlockCommand}</c> without having
    /// to reach an ancestor for the VM &#8212; awkward in compiled bindings.
    /// </summary>
    public IRelayCommand<EnsembleBlockNode> SelectBlockCommand { get; }

    public string Type { get; }

    public string Title { get; }

    public IReadOnlyList<EnsembleDetailItem> Modifiers { get; }

    public IReadOnlyList<EnsembleDetailItem> Properties { get; }

    public IReadOnlyList<EnsembleBlockNode> Children { get; }

    /// <summary>
    /// Hex color (e.g. <c>#3B82F6</c>) for the left accent stripe. The view layer
    /// converts this to a SolidColorBrush (WinUI) or IBrush (future hosts).
    /// </summary>
    public string AccentColorHex { get; }

    public int Depth { get; }

    public bool HasChildren => Children.Count > 0;

    public bool HasModifiers => Modifiers.Count > 0;

    public bool HasProperties => Properties.Count > 0;

    /// <summary>One-liner shown in the header ("3 properties, 2 children").</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>(3);
            if (Modifiers.Count > 0)
                parts.Add($"{Modifiers.Count} modifier{(Modifiers.Count == 1 ? string.Empty : "s")}");
            if (Properties.Count > 0)
                parts.Add($"{Properties.Count} propert{(Properties.Count == 1 ? "y" : "ies")}");
            if (Children.Count > 0)
                parts.Add($"{Children.Count} child{(Children.Count == 1 ? string.Empty : "ren")}");
            return parts.Count == 0 ? "empty" : string.Join(" · ", parts);
        }
    }

    [ObservableProperty]
    private bool _isExpanded;

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}

/// <summary>
/// Displays the ensemble configuration file as a tree of typed, colored
/// blocks. Read-only; the view fetches the file path through the hidden
/// <c>tw config get-path</c> command and parses the file client-side so the
/// result is identical to what the coordinator sees at startup (after
/// include resolution, interpolation, and <c>if</c> pruning).
/// </summary>
public sealed partial class EnsembleViewModel : CategoryViewModelBase
{
    // 10 mid-saturation hues picked to stay legible on both Light and Dark
    // Fluent themes. The order is arbitrary; colors are assigned to block types
    // on first-seen basis, so no type is "hard-coded" to a particular color.
    private static readonly IReadOnlyList<string> Palette = new[]
    {
        "#3B82F6", // blue
        "#10B981", // emerald
        "#F59E0B", // amber
        "#8B5CF6", // violet
        "#EC4899", // pink
        "#14B8A6", // teal
        "#F97316", // orange
        "#06B6D4", // cyan
        "#EF4444", // rose
        "#84CC16", // lime
    };

    private const string FallbackAccentHex = CliPalette.NeutralHex;

    private readonly ITwCli _cli;

    public EnsembleViewModel(ITwCli cli, IUiDispatcher dispatcher) : base(dispatcher)
    {
        _cli = cli;
    }

    public override string Title => "Ensemble";

    // Segoe Fluent Icons glyph: Page (E7C3), matches the "file of configuration" feel.
    public override string Icon => "\uE7C3";

    public ObservableCollection<EnsembleBlockNode> Blocks { get; } = new();

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private EnsembleBlockNode? _selected;

    public bool IsDrawerOpen => Selected is not null;

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
        => await RefreshAsync(cancellationToken);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var path = await GetEnsemblePathAsync(cancellationToken).ConfigureAwait(false);
            var document = await EnsembleDocumentParser.LoadFileAsync(path, cancellationToken)
                .ConfigureAwait(false);

            var built = BuildTree(document, SelectBlockCommand);

            Dispatcher.Post(() =>
            {
                SourcePath = path;
                Selected = null;
                Blocks.Clear();
                foreach (var node in built)
                    Blocks.Add(node);
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
    private void SelectBlock(EnsembleBlockNode? block)
    {
        // Null parameter happens when the command is wired to a Button that
        // has no CommandParameter (should not normally occur); swallowing is
        // safer than throwing in a UI thread.
        if (block is null)
            return;

        Selected = block;
    }

    [RelayCommand]
    private void CloseDrawer() => Selected = null;

    partial void OnSelectedChanged(EnsembleBlockNode? value)
        => OnPropertyChanged(nameof(IsDrawerOpen));

    private async Task<string> GetEnsemblePathAsync(CancellationToken cancellationToken)
    {
        var element = await _cli.RunOneShotAsync(
            new[] { "config", "get-path" }, cancellationToken).ConfigureAwait(false);

        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("path", out var pathEl)
            || pathEl.ValueKind != JsonValueKind.String
            || pathEl.GetString() is not { Length: > 0 } path)
        {
            throw new InvalidOperationException(
                "`tw config get-path` did not return a path. " +
                "Is the coordinator running?");
        }

        return path;
    }

    private static IReadOnlyList<EnsembleBlockNode> BuildTree(
        ConfigDocument document, IRelayCommand<EnsembleBlockNode> selectCommand)
    {
        var typeColors = new Dictionary<string, string>(StringComparer.Ordinal);
        var topLevel = new List<EnsembleBlockNode>(document.Blocks.Count);
        foreach (var block in document.Blocks)
            topLevel.Add(BuildNode(block, depth: 0, typeColors, selectCommand));
        return topLevel;
    }

    private static EnsembleBlockNode BuildNode(
        ConfigBlock block,
        int depth,
        Dictionary<string, string> typeColors,
        IRelayCommand<EnsembleBlockNode> selectCommand)
    {
        var accent = ResolveColor(block.Type, typeColors);

        var modifiers = new List<EnsembleDetailItem>(block.Modifiers.Count);
        foreach (var mod in block.Modifiers)
            modifiers.Add(new EnsembleDetailItem(mod.Key, mod.Value.ToString() ?? string.Empty));

        var properties = new List<EnsembleDetailItem>(block.Properties.Count);
        foreach (var prop in block.Properties)
            properties.Add(new EnsembleDetailItem(prop.Key, prop.Value.ToString() ?? string.Empty));

        var children = new List<EnsembleBlockNode>(block.Children.Count);
        foreach (var child in block.Children)
            children.Add(BuildNode(child, depth + 1, typeColors, selectCommand));

        var title = string.IsNullOrWhiteSpace(block.Name) ? "Untitled" : block.Name;

        // Top-level blocks open showing their children; deeper nodes start
        // collapsed so the screen doesn't explode on first render.
        var isExpanded = depth == 0;

        return new EnsembleBlockNode(
            type: string.IsNullOrWhiteSpace(block.Type) ? "block" : block.Type,
            title: title,
            modifiers: modifiers,
            properties: properties,
            children: children,
            accentColorHex: accent,
            depth: depth,
            isExpanded: isExpanded,
            selectCommand: selectCommand);
    }

    private static string ResolveColor(
        string type, Dictionary<string, string> typeColors)
    {
        if (string.IsNullOrEmpty(type))
            return FallbackAccentHex;

        if (typeColors.TryGetValue(type, out var hex))
            return hex;

        // Palette.Count is 10; modulo guarantees color reuse after 10 types
        // are seen, per the spec ("a simple array with 10 colors in loop").
        hex = Palette[typeColors.Count % Palette.Count];
        typeColors.Add(type, hex);
        return hex;
    }
}
