using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Telemetry;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// A fully built runner application ready to execute. Created by
/// <see cref="RunnerBuilder.BuildAsync"/> after all customization
/// has been applied. The run phase is identical for every runner:
/// start the host and sentinel, invoke runlet lifecycle hooks,
/// notify ready, wait for shutdown, then stop runlets.
/// </summary>
public sealed class RunnerApp
{
    private readonly IHost _host;
    private readonly RunnerOptions _options;
    private readonly CoordinatorPipeClient _client;
    private readonly IReadOnlyList<RunletState> _runlets;
    private readonly ILogger _logger;

    internal RunnerApp(
        IHost host,
        RunnerOptions options,
        CoordinatorPipeClient client,
        IReadOnlyList<RunletState> runlets)
    {
        _host = host;
        _options = options;
        _client = client;
        _runlets = runlets;
        _logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger<RunnerApp>();
    }

    /// <summary>
    /// Runs the standard runner lifecycle and returns the process exit code.
    /// </summary>
    public async Task<int> RunAsync()
    {
        using var sentinel = new SentinelPipeClient(
            _options.SentinelPipe,
            _host.Services.GetRequiredService<IHostApplicationLifetime>(),
            _host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger<SentinelPipeClient>());

        bool started;
        using (OtTraces.Source.StartActivity(OtTraces.StartHost))
            started = await TryStartHostAsync(sentinel);

        if (!started)
            return 1;

        var appLifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
        var stopping = appLifetime.ApplicationStopping;

        using (OtTraces.Source.StartActivity(OtTraces.StartRunlets))
        {
            foreach (var runlet in _runlets)
            {
                using var _ = OtTraces.Source.Start(OtTraces.StartRunlet,
                    (OtTraces.RunletName, runlet.Descriptor.Name));
                await runlet.Instance.StartAsync(_host.Services, stopping);
            }
        }

        using (OtTraces.Source.StartActivity(OtTraces.NotifyReady))
            await NotifyReadySafe();

        await _host.WaitForShutdownAsync();

        using (OtTraces.Source.StartActivity(OtTraces.StopRunlets))
        {
            foreach (var runlet in _runlets)
            {
                try
                {
                    using var _ = OtTraces.Source.Start(OtTraces.StopRunlet,
                        (OtTraces.RunletName, runlet.Descriptor.Name));
                    await runlet.Instance.StopAsync(_host.Services, stopping);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Runlet '{Name}' StopAsync failed",
                        runlet.Descriptor.Name);
                }
            }
        }

        return 0;
    }

    private async Task<bool> TryStartHostAsync(SentinelPipeClient sentinel)
    {
        try
        {
            await _host.StartAsync();
            await sentinel.StartAsync(CancellationToken.None);
            return true;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Host startup failed");
            try
            {
                await _client.NotifyFatalAsync(_options.RunnerId, $"Host startup failed: {ex.Message}");
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception notifyEx)
            {
                _logger.LogTrace(notifyEx, "Failed to notify coordinator of startup failure");
            }
            return false;
        }
    }

    private async Task NotifyReadySafe()
    {
        try
        {
            await _client.NotifyReadyAsync(_options.RunnerId);
            _logger.LogDebug("Runner '{Id}' reported ready", _options.RunnerId);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify coordinator of readiness");
        }
    }
}