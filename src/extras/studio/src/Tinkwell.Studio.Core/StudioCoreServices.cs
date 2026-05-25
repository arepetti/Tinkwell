using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio;

/// <summary>
/// Registers the UI-framework-agnostic pieces of Studio: services, shared state,
/// and view models. UI hosts call this from their own DI setup and then add the
/// host-specific bits (<see cref="IUiDispatcher"/>, <see cref="IThemeService"/>,
/// the main window, logging sinks).
/// </summary>
public static class StudioCoreServices
{
    public static IServiceCollection AddStudioCore(this IServiceCollection services)
    {
        services.AddSingleton<StudioSettings>();
        services.AddSingleton<CommandLog>();
        services.AddSingleton<ITwCli, TwCliProcessRunner>();
        services.AddSingleton<ICoordinatorHeartbeat, CoordinatorHeartbeat>();
        services.AddSingleton<ICoordinatorProbe, CoordinatorProbe>();
        services.AddSingleton<IConnectionStore, ConnectionStore>();
        services.AddSingleton<MqttMonitorService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<RunnersViewModel>();
        services.AddSingleton<ServicesViewModel>();
        services.AddSingleton<StoreViewModel>();
        services.AddSingleton<MeasuresViewModel>();
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<MqttViewModel>();
        services.AddSingleton<CoapViewModel>();
        services.AddSingleton<EnsembleViewModel>();
        services.AddSingleton<CommandLogViewModel>();

        // The connection dialog is shown once at startup, so its view model is
        // a transient: each app launch instantiates a fresh form state.
        services.AddTransient<ConnectionDialogViewModel>();

        return services;
    }
}
