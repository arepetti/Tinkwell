using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bootstrapper;

public interface IDllHost
{
    string RunnerName { get; }
    IDictionary<string, object> Properties { get; }
    IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
}
