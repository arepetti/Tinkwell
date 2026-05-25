using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Wallclock;

public sealed class WallclockRunlet : IRunlet
{
    public static string Name => "wallclock";

    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        services.AddSingleton(WallclockConfig.Parse(settings));
        services.AddHostedService<WallclockWorker>();
    }
}
