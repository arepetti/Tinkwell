using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Sample.GrpcMeasureReader;

/// <summary>
/// A gRPC runlet that reads a single measure via the Tinkwell Measures gRPC
/// service. Discovers the measures service through <see cref="IServiceDiscovery"/>
/// so it can live in its own runner — no co-hosting required.
/// </summary>
public sealed class MeasureReaderRunlet : IGrpcRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var measureName = settings["measure"] ?? "temperature";
        services.AddSingleton(new MeasureReaderOptions(measureName));
    }

    public void MapGrpcServices(IServiceCollection services)
    {
        services.AddSingleton<MeasureReaderGrpcService>();
    }

    public void MapGrpcEndpoints(IGrpcEndpointMapper mapper)
    {
        mapper.MapService<MeasureReaderGrpcService>(opts =>
        {
            opts.FriendlyName = "Measure Reader";
            opts.FamilyName = "measure-reader";
        });
    }
}

public sealed record MeasureReaderOptions(string MeasureName);
