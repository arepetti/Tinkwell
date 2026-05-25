using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Sample.HeadlessMeasureWatcher;

/// <summary>
/// A headless runlet that watches the Tinkwell Measures service for value
/// changes via the <c>Watch</c> gRPC streaming RPC and prints them to the
/// console. Discovers the measures service through <see cref="IServiceDiscovery"/>
/// so it can live in its own runner — no co-hosting required.
/// </summary>
public sealed class MeasureWatcherRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var prefix = settings["prefix"];
        services.AddSingleton(new MeasureWatcherOptions(prefix));
        services.AddHostedService<MeasureWatcherWorker>();
    }
}

public sealed record MeasureWatcherOptions(string? Prefix);
