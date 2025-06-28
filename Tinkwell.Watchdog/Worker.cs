using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

sealed class Worker(
    IConfiguration configuration,
    MonitoringOptions options,
    INamedPipeClient pipeClient,
    ILogger<Worker> logger) : BackgroundService, IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        // Shutdown all channels gracefully (waiting for ongoing operations to complete).
        var shutdownTasks = new List<Task>();

        if (_discoveryChannel is not null)
            shutdownTasks.Add(_discoveryChannel.ShutdownAsync());

        if (_storeChannel is not null)
            shutdownTasks.Add(_storeChannel.ShutdownAsync());

        foreach (var channel in _channelCache.Values)
            shutdownTasks.Add(channel.ShutdownAsync());

        await Task.WhenAll(shutdownTasks);

        // Dispose all the resources
        _discoveryChannel?.Dispose();
        _storeChannel?.Dispose();

        foreach (var channel in _channelCache.Values)
            channel.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var discoveryHostAddress = await HostingInformation.ResolveDiscoveryServiceAddressAsync(_configuration, _pipeClient);
            _discoveryChannel = GrpcChannel.ForAddress(discoveryHostAddress);

            using var timer = new PeriodicTimer(_options.Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await CollectHealthDataAsync(stoppingToken);
                }
                catch (Exception e)
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
    private readonly ConcurrentDictionary<string, GrpcChannel> _channelCache = new();
    private readonly ConcurrentDictionary<string, string> _nameMap = new();
    private GrpcChannel? _discoveryChannel;
    private GrpcChannel? _storeChannel;

    private async Task CollectHealthDataAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_discoveryChannel is not null);

        var (discovery, store) = await CreateClientsAsync();
        var discoveryResponse = await discovery.FindAllAsync(new() { FamilyName = HealthCheck.Descriptor.Name }, cancellationToken: cancellationToken);
        foreach (var host in discoveryResponse.Hosts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var healthCheckService = new HealthCheck.HealthCheckClient(GetOrCreateChannel(host));
                var healthResponse = await healthCheckService.CheckAsync(new(), cancellationToken: cancellationToken);
                await StoreMeasureAsync(store, healthResponse.Name, healthResponse.Status, cancellationToken);
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
                    {
                        await StoreMeasureAsync(store,
                            runnerName,
                            HealthCheckResponse.Types.ServingStatus.NotServing,
                            cancellationToken);
                    }
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

    private async Task<(Discovery.DiscoveryClient discovery, Store.StoreClient store)> CreateClientsAsync()
    {
        var discovery = new Discovery.DiscoveryClient(_discoveryChannel);

        if (_storeChannel is null)
        {
            var storeHost = await discovery.FindAsync(new DiscoveryFindRequest { Name = Store.Descriptor.FullName }, cancellationToken: cancellationToken);
            _storeChannel = GrpcChannel.ForAddress(storeHost.Host);
        }

        var store = new Store.StoreClient(_storeChannel);

        return (discovery, store);
    }

    private async Task StoreMeasureAsync(
        Store.StoreClient store,
        string runnerName,
        HealthCheckResponse.Types.ServingStatus status,
        CancellationToken cancellationToken)
    {
        var measureName = await GetOrRegisterMeasureNameAsync(store, runnerName, cancellationToken);
        var value = Convert.ToString((int)status, CultureInfo.InvariantCulture) ?? "";
        await store.UpdateAsync(new() { Name = measureName, Value = value }, cancellationToken: cancellationToken);
    }

    private async Task<string> GetOrRegisterMeasureNameAsync(Store.StoreClient store, string runnerName, CancellationToken cancellationToken)
    {
        // _nameMap contains the name of the measure associated with each runner
        if (_nameMap.TryGetValue(runnerName, out var measureName))
            return measureName;

        var newMeasureName = _options.NamePattern.Replace("{{ name }}", runnerName);
        await store.RegisterAsync(new()
        {
            Name = newMeasureName,
            QuantityType = "Scalar",
            Unit = "",
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