using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Bootstrapper.Hosting;

public interface IGrpcServerHost : IConfigurableHost
{
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
