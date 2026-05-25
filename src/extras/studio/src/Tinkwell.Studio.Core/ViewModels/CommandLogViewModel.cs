using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

/// <summary>
/// View model for the Command Log category. The log itself is owned by
/// <see cref="CommandLog"/> (a singleton fed by <see cref="TwCliProcessRunner"/>),
/// so this VM is mostly a thin shell over the shared collection plus the
/// usual selection / drawer plumbing every category uses.
/// </summary>
public sealed partial class CommandLogViewModel : CategoryViewModelBase
{
    public CommandLogViewModel(CommandLog log, IUiDispatcher dispatcher) : base(dispatcher)
    {
        Log = log;
    }

    public override string Title => "Command log";

    // Segoe Fluent Icons glyph: CommandPrompt (E756).
    public override string Icon => "\uE756";

    /// <summary>
    /// Bound directly by the view; <see cref="CommandLog"/> mutates this
    /// collection on the dispatcher thread, so no copy is needed.
    /// </summary>
    public CommandLog Log { get; }

    [ObservableProperty]
    private CommandLogEntry? _selected;

    public bool IsDrawerOpen => Selected is not null;

    [RelayCommand]
    private void CloseDrawer() => Selected = null;

    [RelayCommand]
    private void Clear()
    {
        Log.Clear();
        Selected = null;
    }

    partial void OnSelectedChanged(CommandLogEntry? value)
        => OnPropertyChanged(nameof(IsDrawerOpen));
}
