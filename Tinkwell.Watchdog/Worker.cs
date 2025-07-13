using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Measures;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

sealed class Worker(
    IConfiguration configuration,
    MonitoringOptions options,
    INamedPipeClient pipeClient,
    ILogger<Worker> logger,
    ServiceLocator locator) : BackgroundService, IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        // Shutdown all channels gracefully (waiting for ongoing operations to complete).
        var shutdownTasks = new List<Task>();

        if (_store?.Channel is not null)
            shutdownTasks.Add(_store.Channel.ShutdownAsync());

        foreach (var channel in _channelCache.Values)
            shutdownTasks.Add(channel.ShutdownAsync());

        await Task.WhenAll(shutdownTasks);

        _store?.Channel?.Dispose();

        foreach (var channel in _channelCache.Values)
            channel.Dispose();

        await _locator.DisposeAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _discovery = await _locator.FindDiscoveryAsync(stoppingToken);
            _store = await _locator.FindStoreAsync(stoppingToken);

            using var timer = new PeriodicTimer(_options.Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await CollectHealthDataAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "An unexpected error occurred while collecting health data. The worker will try again on the next run.");
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

    private readonly IConfiguration _configuration = configuration;
    private readonly MonitoringOptions _options = options;
    private readonly INamedPipeClient _pipeClient = pipeClient;
    private readonly ILogger<Worker> _logger = logger;
    private readonly ServiceLocator _locator = locator;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channelCache = new();
    private readonly ConcurrentDictionary<string, string> _nameMap = new();
    private Discovery.DiscoveryClient? _discovery;
    private GrpcService<Store.StoreClient>? _store;

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
                await StoreMeasureAsync(healthResponse.Name, healthResponse.Status, cancellationToken);
            }
            catch (RpcException exception)
            {
                _logger.LogError(exception, "Failed to check health for host {HostAddress}.", host);

                // If the error is internal or we have a problem with the connection then we can safely
                // assume the service is down and register it as such.
                if (IsConnectionOrChannelError(exception))
                {
                    var runnerName = await ResolveRunnerNameFromAddressAsync(host, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(runnerName))
                        await StoreMeasureAsync(runnerName, HealthCheckResponse.Types.ServingStatus.NotServing, cancellationToken);
                }
            }
        }
    }

    private GrpcChannel GetOrCreateChannel(string address) =>
        _channelCache.GetOrAdd(address, static x => GrpcChannel.ForAddress(x));

    private bool IsConnectionOrChannelError(RpcException exception)
    {
        return exception.StatusCode == StatusCode.Unavailable ||
               exception.StatusCode == StatusCode.DeadlineExceeded ||
               exception.StatusCode == StatusCode.Internal;
    }

    private async Task StoreMeasureAsync(string runnerName, HealthCheckResponse.Types.ServingStatus status, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);
        
        var measureName = await GetOrRegisterMeasureNameAsync(runnerName, cancellationToken);
        var value = status.ToString();
        await _store.Client.AsFacade().WriteStringAsync(measureName, value, cancellationToken);
     }

    private async Task<string> GetOrRegisterMeasureNameAsync(string runnerName, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        // _nameMap contains the name of the measure associated with each runner
        if (_nameMap.TryGetValue(runnerName, out var measureName))
            return measureName;

        var newMeasureName = _options.NamePattern.Replace("{{ name }}", runnerName);
        await _store.Client.RegisterAsync(new()
        {
            Definition = new()
            {
                Type = StoreDefinition.Types.Type.String,
                Attributes = 4, // System generated
                Name = newMeasureName,
            },
            Metadata = new()
            {
                Description = $"Service satus for {measureName}",
                Tags = { "HealthCheck" }
            }
        }, cancellationToken: cancellationToken);

        _nameMap.TryAdd(runnerName, newMeasureName);
        return newMeasureName;
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
}