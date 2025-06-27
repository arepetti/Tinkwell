using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Bootstrapper.Rpc;

public interface IGrpcServerHost
{
    IServiceCollection Services { get; }
    string RunnerName { get; }
    IDictionary<string, object> Properties { get; }
    void MapGrpcService<TService>(ServiceDefinition? definition = default) where TService : class;
}
