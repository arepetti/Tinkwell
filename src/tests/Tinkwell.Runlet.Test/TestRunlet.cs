using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Test;

/// <summary>
/// A test runlet whose behavior is controlled by the <c>mode</c> setting
/// in the <c>.tw</c> configuration file. Used by integration tests to
/// simulate various runner scenarios without custom executables.
/// </summary>
/// <remarks>
/// Supported modes:
/// <list type="bullet">
///   <item><c>ready</c> (default) — no-op, runner proceeds normally.</item>
///   <item><c>crash-on-start</c> — throws during service registration.</item>
///   <item><c>crash-after-ready</c> — runner reports ready, then exits
///     after <c>crash-delay-ms</c> milliseconds (default 1000).</item>
///   <item><c>hang</c> — blocks host startup indefinitely so
///     <c>notify ready</c> is never sent.</item>
/// </list>
/// </remarks>
public sealed class TestRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var mode = settings["mode"] ?? "ready";

        switch (mode)
        {
            case "ready":
                break;

            case "crash-on-start":
                throw new InvalidOperationException("TestRunlet: crash-on-start");

            case "crash-after-ready":
                var delayMs = int.TryParse(settings["crash-delay-ms"], out var d) ? d : 1000;
                services.AddSingleton(new CrashAfterReadyOptions(delayMs));
                services.AddHostedService<CrashAfterReadyService>();
                break;

            case "hang":
                services.AddHostedService<HangService>();
                break;

            default:
                throw new InvalidOperationException($"TestRunlet: unknown mode '{mode}'");
        }
    }
}

internal sealed record CrashAfterReadyOptions(int DelayMs);

/// <summary>
/// Waits for the configured delay then terminates the process with a
/// non-zero exit code, simulating a crash after the runner has reported ready.
/// </summary>
internal sealed class CrashAfterReadyService(CrashAfterReadyOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(options.DelayMs, stoppingToken);
        Environment.Exit(1);
    }
}

/// <summary>
/// Blocks host startup indefinitely by never completing <see cref="StartAsync"/>,
/// preventing the runner from ever sending <c>notify ready</c>.
/// </summary>
internal sealed class HangService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Delay(Timeout.Infinite, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
