using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tinkwell.Bootstrapper;

public interface IDllHost : ITinkwellHostRunnerBase
{
    IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
}
