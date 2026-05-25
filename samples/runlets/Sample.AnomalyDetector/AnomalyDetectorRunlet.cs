using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Sample.AnomalyDetector;

/// <summary>
/// A headless runlet that watches measures for anomalous values using a
/// simple z-score detector (Mahalanobis distance in 1D). When a value
/// deviates beyond a configurable threshold, it publishes a <c>Fired</c>
/// event to the event bus.
/// </summary>
public sealed class AnomalyDetectorRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var options = new AnomalyDetectorOptions(
            Threshold: double.TryParse(settings["threshold"], out var t) ? t : 3.0,
            WindowSize: int.TryParse(settings["window-size"], out var w) ? w : 50,
            Prefix: settings["prefix"]);

        services.AddSingleton(options);
        services.AddHostedService<AnomalyDetectorWorker>();
    }
}

public sealed record AnomalyDetectorOptions(
    double Threshold,
    int WindowSize,
    string? Prefix);
