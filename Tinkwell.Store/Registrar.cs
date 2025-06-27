using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper;
using Tinkwell.Store.Services;

namespace Tinkwell.Store;

public sealed class Registrar : IHostedGrpcServerRegistrar
{
    public void ConfigureRoutes(IGrpcServerHost host)
    {
        host.MapGrpcService<StoreService>();
    }

    public void ConfigureServices(IGrpcServerHost host)
    {
        host.Services.AddSingleton<IRegistry, Registry>();
    }
}
