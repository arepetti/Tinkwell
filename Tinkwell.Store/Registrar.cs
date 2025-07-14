using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures;
using Tinkwell.Measures.Storage;
using Tinkwell.Store.Services;

namespace Tinkwell.Store;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        string name = Tinkwell.Services.Store.Descriptor.Name;
        host.MapGrpcService<StoreService>(new ServiceDefinition { FamilyName = name, Aliases = [name] });
    }

    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IStorage, InMemoryStorage>();
            services.AddSingleton<IRegistry, Registry>();
        });
    }
}
