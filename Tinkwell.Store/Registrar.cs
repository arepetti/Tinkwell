using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Store.Services;

namespace Tinkwell.Store;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        string name = Tinkwell.Services.Store.Descriptor.Name;
        host.MapGrpcService<StoreService>(new ServiceDefinition { FamilyName = name, Aliases = [name] });
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton<IRegistry, Registry>();
    }
}
