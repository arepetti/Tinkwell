using CommunityToolkit.Mvvm.ComponentModel;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public abstract partial class CategoryViewModelBase : ObservableObject
{
    protected CategoryViewModelBase(IUiDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    /// <summary>
    /// Marshals mutations back to the UI thread. Exposed to subclasses so worker-thread
    /// work (CLI calls, MQTT callbacks) can update observable collections safely.
    /// </summary>
    protected IUiDispatcher Dispatcher { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Free-text query bound to the global search box in the title bar.
    /// Subclasses that opt into search override <see cref="SupportsSearch"/>
    /// and react to changes via <see cref="OnSearchTextUpdated"/>.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => OnSearchTextUpdated(value);

    public abstract string Title { get; }

    public abstract string Icon { get; }

    /// <summary>
    /// When <c>true</c> the global search box in the title bar is shown while
    /// this category is active. Defaults to <c>false</c> for views that have
    /// nothing to filter.
    /// </summary>
    public virtual bool SupportsSearch => false;

    /// <summary>
    /// Placeholder text rendered inside the global search box. Only consulted
    /// when <see cref="SupportsSearch"/> returns <c>true</c>.
    /// </summary>
    public virtual string SearchPlaceholder => "Search";

    public virtual Task OnActivatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual Task OnDeactivatedAsync() => Task.CompletedTask;

    /// <summary>
    /// Hook fired whenever <see cref="SearchText"/> changes. Default is a no-op;
    /// search-aware subclasses override to re-apply their filter.
    /// </summary>
    protected virtual void OnSearchTextUpdated(string value)
    {
    }

    protected void SetError(Exception ex)
        => ErrorMessage = ex.Message;

    protected void ClearError()
        => ErrorMessage = null;
}
