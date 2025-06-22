using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Bootstrapper.Rpc;

public interface IGrpcServerHost
{
    IServiceCollection Services { get; }
    void MapGrpcService<TService>() where TService : class;
}
