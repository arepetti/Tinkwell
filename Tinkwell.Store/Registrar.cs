using Grpc.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures;
using Tinkwell.Measures.Storage;
using Tinkwell.Store.Services;

namespace Tinkwell.Store;

public sealed class Registrar(ILogger<Registrar> logger) : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        string name = Tinkwell.Services.Store.Descriptor.Name;
        host.MapGrpcService<StoreService>(new ServiceDefinition { FamilyName = name, Aliases = [name] });
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        var storageStrategy = ResolveStrategy<IStorage, InMemoryStorage>(host, "Storage", "storage_strategy");
        var registryStrategy = ResolveStrategy<IRegistry, Registry>(host, "Registry", "registry_strategy");

        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(typeof(IStorage), storageStrategy);
            services.AddSingleton(typeof(IRegistry), registryStrategy);
        });
    }

    private ILogger<Registrar> _logger = logger;

    private Type ResolveStrategy<TContract, TDefaultImplementation>(
        IConfigurableHost host, string taskName, string settingPropertyName)
        where TDefaultImplementation : TContract
    {
        var requestedStrategyName = host.GetPropertyString(settingPropertyName, null);
        if (string.IsNullOrWhiteSpace(requestedStrategyName))
            return typeof(TDefaultImplementation);

        _logger.LogInformation("Searching for strategy {Strategy} for task {Task}", requestedStrategyName, taskName);

        var resolver = StrategyAssemblyLoader
            .LoadAssemblies(typeof(Registrar).Namespace!, taskName)
            .SelectMany(StrategyAssemblyLoader.FindTypesImplementing<IStrategyImplementationResolver>)
            .Select(Activator.CreateInstance)
            .Cast<IStrategyImplementationResolver>()
            .SingleOrDefault(x => requestedStrategyName.Equals(x.GetImplementationName(typeof(TContract)), StringComparison.Ordinal));

        if (resolver is null)
        {
            _logger.LogWarning("Cannot find any implementation with the specified name {Name}. Using default {Default}",
                requestedStrategyName, typeof(TDefaultImplementation).Name);
            return typeof(TDefaultImplementation);
        }


        resolver.ConfigureServices(host);
        var implementation = resolver.GetImplementationType(typeof(TContract))!;

        _logger.LogInformation("Found strategy implementation {StrategyName}", implementation.FullName);
        return implementation;
    }
}
