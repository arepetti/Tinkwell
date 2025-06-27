using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Store.Services;

namespace Tinkwell.Store;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<StoreService>(new ServiceDefinition { Aliases = [ Tinkwell.Services.Store.Descriptor.Name] });
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton<IRegistry, Registry>();
    }
}
