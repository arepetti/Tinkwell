using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;

namespace Tinkwell.Bootstrapper;

public interface IGrpcServerHost : ITinkwellHostRunnerBase
{
    IServiceCollection Services { get; }
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
