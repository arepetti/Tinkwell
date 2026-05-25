using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tinkwell.Studio.Services;

namespace Tinkwell.Studio.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IUiDispatcher _dispatcher;
    private CancellationTokenSource? _activationCts;

    public MainWindowViewModel(
        ICoordinatorHeartbeat heartbeat,
        IUiDispatcher dispatcher,
        HomeViewModel home,
        RunnersViewModel runners,
        ServicesViewModel services,
        StoreViewModel store,
        MeasuresViewModel measures,
        EventsViewModel events,
        MqttViewModel mqtt,
        CoapViewModel coap,
        EnsembleViewModel ensemble,
        CommandLogViewModel commandLog)
    {
        _dispatcher = dispatcher;

        Categories = new ObservableCollection<CategoryViewModelBase>
        {
            home, events, measures, store, runners, services, mqtt, coap, ensemble, commandLog,
        };

        _selectedCategory = Categories[0];
        ApplyCoordinatorStatus(heartbeat.Current);

        heartbeat.Changed += (_, status) =>
            _dispatcher.Post(() => ApplyCoordinatorStatus(status));
        heartbeat.Start();

        _ = ActivateAsync(_selectedCategory);
    }

    public ObservableCollection<CategoryViewModelBase> Categories { get; }

    [ObservableProperty]
    private CategoryViewModelBase _selectedCategory;

    [ObservableProperty]
    private string _coordinatorBanner = "Checking coordinator...";

    [ObservableProperty]
    private bool _coordinatorIsOnline;

    [ObservableProperty]
    private bool _coordinatorIsOffline;

    partial void OnSelectedCategoryChanged(CategoryViewModelBase? oldValue, CategoryViewModelBase newValue)
    {
        _ = SwitchCategoryAsync(oldValue, newValue);
    }

    private async Task SwitchCategoryAsync(CategoryViewModelBase? previous, CategoryViewModelBase next)
    {
        if (previous is not null)
            await previous.OnDeactivatedAsync();
        await ActivateAsync(next);
    }

    private async Task ActivateAsync(CategoryViewModelBase category)
    {
        _activationCts?.Cancel();
        _activationCts?.Dispose();
        _activationCts = new CancellationTokenSource();
        try
        {
            await category.OnActivatedAsync(_activationCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyCoordinatorStatus(CoordinatorStatus status)
    {
        CoordinatorIsOnline = status.Connectivity == CoordinatorConnectivity.Online;
        CoordinatorIsOffline = status.Connectivity == CoordinatorConnectivity.Offline;
        // The status banner intentionally does not surface the heartbeat latency:
        // the indicator dot already conveys reachability and the value churned
        // every poll without adding actionable information.
        CoordinatorBanner = status.Connectivity switch
        {
            CoordinatorConnectivity.Online => "Coordinator online",
            CoordinatorConnectivity.Offline => status.LastError is null
                ? "Coordinator unreachable"
                : $"Coordinator unreachable: {Shorten(status.LastError)}",
            _ => "Checking coordinator...",
        };
    }

    private static string Shorten(string message)
    {
        const int max = 120;
        var single = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return single.Length <= max ? single : single[..max] + "...";
    }
}
