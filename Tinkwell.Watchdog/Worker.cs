using System.Collections.Concurrent;
using System.Globalization;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Services;

namespace Tinkwell.Watchdog;

sealed class Worker(IConfiguration configuration, MonitoringOptions options, INamedPipeClient pipeClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var discoveryHostAddress = await HostingInformation.ResolveDiscoveryServiceAddressAsync(_configuration, _pipeClient);
            _discoveryChannel = GrpcChannel.ForAddress(discoveryHostAddress);

            var timer = new PeriodicTimer(_options.Interval);
            while (await timer.WaitForNextTickAsync())
            {
                await CollectHealthDataAsync();
            }
        });
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private readonly IConfiguration _configuration = configuration;
    private readonly MonitoringOptions _options = options;
    private readonly INamedPipeClient _pipeClient = pipeClient;
    private GrpcChannel? _discoveryChannel;
    private GrpcChannel? _storeChannel;
    private static readonly ConcurrentDictionary<string, GrpcChannel> _channelCache = new();
    private static readonly ConcurrentDictionary<string, string> _nameMap = new();

    private async Task CollectHealthDataAsync()
    {
        var discovery = new Discovery.DiscoveryClient(_discoveryChannel);

        if (_storeChannel is null)
        {
            var storeHost = await discovery.FindAsync(new DiscoveryFindRequest { Name = Store.Descriptor.FullName });
            _storeChannel = GrpcChannel.ForAddress(storeHost.Host);
        }

        var store = new Store.StoreClient(_storeChannel);

        var hostsToCheck = await discovery.FindAllAsync(new() { FamilyName = HealthCheck.Descriptor.Name });
        foreach (var host in hostsToCheck.Hosts)
        {
            var service = new HealthCheck.HealthCheckClient(GetOrCreateChannel(host));
            var response = await service.CheckAsync(new HealthCheckRequest());

            var name = _nameMap.GetOrAdd(response.Name, x => {
                var measureName = FormatMeasureName(x);
                store.Register(new()
                {
                    Name = measureName,
                    QuantityType = "Scalar",
                    Unit = "",
                });
                return measureName;
            });

            var value = Convert.ToString((int)response.Status, CultureInfo.InvariantCulture) ?? "";
            await store.UpdateAsync(new() { Name = name, Value = value });
        }
    }

    private static GrpcChannel GetOrCreateChannel(string address)
        => _channelCache.GetOrAdd(address, x => GrpcChannel.ForAddress(x));

    private string FormatMeasureName(string name)
        => _options.NamePattern.Replace("{{ name }}", name);
}