using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Bootstrapper;

public interface IGrpcServerHost : ITinkwellHostRunnerBase
{
    IServiceCollection Services { get; }
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
