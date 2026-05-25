using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Tinkwell.Studio.Services;
using Tinkwell.Studio.ViewModels;
using Tinkwell.Studio.Views;

namespace Tinkwell.Studio;

/// <summary>
/// Application singleton. Builds the DI container, then walks the user through
/// the startup connection dialog before activating <see cref="MainWindow"/>.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    /// <summary>
    /// Global access point to the DI container. Views that XAML cannot wire up
    /// declaratively (e.g. content dialogs created at runtime) can resolve from
    /// here; most consumers should use constructor injection instead.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Convenience accessor used by xaml-generated bindings and by code-behind.
    /// </summary>
    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // The connection flow is async (file IO + process spawn) but
        // OnLaunched can't be awaited. Kick off the start-up sequence and let
        // it drive window activation on completion.
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        var connectionStore = Services.GetRequiredService<IConnectionStore>();
        var settings = Services.GetRequiredService<StudioSettings>();

        var saved = await connectionStore.LoadAsync().ConfigureAwait(true);

        // Loop locally is unnecessary: the ConnectionDialogViewModel keeps the
        // window open across failed probes and only fires Connected once the
        // probe succeeds. We just await a single close.
        var dialog = Services.GetRequiredService<ConnectionWindow>();
        dialog.LoadDefaults(saved);
        dialog.Activate();

        var chosen = await dialog.ClosedAsync.ConfigureAwait(true);
        if (chosen is null)
        {
            // User clicked Quit (or closed the window). Exit cleanly.
            Exit();
            return;
        }

        settings.Apply(chosen);
        await connectionStore.SaveAsync(chosen).ConfigureAwait(true);

        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddStudioCore();

        // UI-host-specific plumbing: dispatcher and theme toggle.
        services.AddSingleton<IUiDispatcher, WinUiDispatcher>();
        services.AddSingleton<IThemeService, WinUiThemeService>();

        services.AddTransient<MainWindow>();
        services.AddTransient<ConnectionWindow>();
    }
}
