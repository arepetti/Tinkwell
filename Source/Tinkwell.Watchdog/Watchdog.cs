using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

sealed class Watchdog(
    IConfiguration configuration,
    MonitoringOptions options,
    INamedPipeClient pipeClient,
    ILogger<Watchdog> logger,
    ServiceLocator locator) : IWatchdog
{
    public Snapshot[] GetSnapshots()
        => _healthData.GetSnapshots();

    public bool? IsLatestSampleAnAnomaly
        => _healthData.IsLatestSampleAnAnomaly;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _discovery = await _locator.FindDiscoveryAsync(stoppingToken);
            await FetchRunnerListAsync(stoppingToken);
            _logger.LogInformation("Watchdog started successfully");

            using var timer = new PeriodicTimer(_options.Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                _healthData.BeginUpdate();
                try
                {
                    if (_dirty)
                        await FetchRunnerListAsync(stoppingToken);

                    await ProfileAsync(stoppingToken);
                    await CollectHealthDataAsync(stoppingToken);

                    _dirty = _healthData.GetSnapshots().Any(x => x.Quality <= SnapshotQuality.Undetermined);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "An unexpected error occurred while collecting health data. The worker will try again on the next run.");
                }
                finally
                {
                    _healthData.EndUpdate();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "An unexpected error occurred while collecting health data. It's a fatal error and it'll stop.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Shutdown all channels gracefully (waiting for ongoing operations to complete).
        var shutdownTasks = new List<Task>();
        foreach (var channel in _channelCache.Values)
            shutdownTasks.Add(channel.ShutdownAsync());
        await Task.WhenAll(shutdownTasks);

        foreach (var channel in _channelCache.Values)
            channel.Dispose();
    }

    private readonly IConfiguration _configuration = configuration;
    private readonly MonitoringOptions _options = options;
    private readonly INamedPipeClient _pipeClient = pipeClient;
    private readonly ILogger<Watchdog> _logger = logger;
    private readonly ServiceLocator _locator = locator;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channelCache = new();
    private Discovery.DiscoveryClient? _discovery;
    private bool _dirty = true;
    private readonly HealthData _healthData = new();

    private async Task CollectHealthDataAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_discovery is not null);

        var discoveryResponse = await _discovery.FindAllAsync(new() { FamilyName = HealthCheck.Descriptor.Name }, cancellationToken: cancellationToken);
        foreach (var host in discoveryResponse.Hosts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var healthCheckService = new HealthCheck.HealthCheckClient(GetOrCreateChannel(host));
                var healthResponse = await healthCheckService.CheckAsync(new(), cancellationToken: cancellationToken);
                _healthData.UpdateOrAdd(healthResponse.Name, healthResponse.Status.Translate());
            }
            catch (RpcException exception)
            {
                // If the error is internal or we have a problem with the connection then we can safely
                // assume the service is down and register it as such.
                if (IsConnectionOrChannelError(exception))
                {
                    var runnerName = await ResolveRunnerNameFromAddressAsync(host, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(runnerName))
                        _healthData.UpdateOrAdd(runnerName, ServiceStatus.Crashed);
                }
            }
        }
    }

    private GrpcChannel GetOrCreateChannel(string address) =>
        _channelCache.GetOrAdd(address, _locator.CreateChannel);

    private bool IsConnectionOrChannelError(RpcException exception)
    {
        return exception.StatusCode == StatusCode.Unavailable ||
               exception.StatusCode == StatusCode.DeadlineExceeded ||
               exception.StatusCode == StatusCode.Internal;
    }

    private async Task<string?> ResolveRunnerNameFromAddressAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            _pipeClient.Connect(WellKnownNames.SupervisorCommandServerPipeName);
            return await _pipeClient.SendCommandToSupervisorAndDisconnectAsync(
                _configuration,
                "endpoints query \"{address}\" --inverse",
                cancellationToken);
        }
        catch (Exception e)
        {
            // No big deal if something went wrong and we can't resolve the runner name.
            _logger.LogWarning(e, "Failed to resolve runner name from address {Address}.", address);
            return null;
        }
    }

    private async Task FetchRunnerListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pids = await _pipeClient.SendCommandToSupervisorAndDisconnectAsync(_configuration, "runners pids", cancellationToken);
            if (pids is null || pids.StartsWith("Error"))
            {
                _logger.LogWarning("Cannot read the list of active processes: {Reason}", pids);
                return;
            }

            var runners = pids
                .Split(',')
                .Select(entry =>
                {
                    var parts = entry.Split('#');
                    bool isSupervisor = parts[0].Equals("*", StringComparison.Ordinal);
                    string name = isSupervisor ? "Supervisor*" : parts[0];
                    int.TryParse(parts[1], CultureInfo.InvariantCulture, out var pid);
                    return new Runner(name, pid, isSupervisor ? RunnerRole.Supervisor : RunnerRole.Firmlet);
                });

            _healthData.AddRange(runners);

            if (runners.Any())
                _dirty = false;

        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot read the list of active processes: {Reason}", e.Message);
        }
    }

    private async Task ProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Profiler.ProfileAsync(_healthData.GetSnapshots(), cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot collect stats about active processes: {Reason}", e.Message);
        }
    }
}
