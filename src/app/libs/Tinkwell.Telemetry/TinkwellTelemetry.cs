using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Tinkwell.Telemetry;

/// <summary>
/// Extension methods to register the OpenTelemetry SDK with caller-specified
/// activity sources and meters. By default no exporter is configured;
/// set a non-empty <c>Telemetry:OtlpEndpoint</c> configuration value to
/// enable the OTLP exporter.
/// </summary>
/// <example>
/// Register telemetry during host setup (no exporter unless configured):
/// <code>
/// services.AddTinkwellTelemetry(
///     configuration,
///     sourceNames: [OtTraces.SourceName],
///     meterNames:  [OtMetrics.MeterName]);
/// </code>
/// Then add the OTLP endpoint in <c>appsettings.json</c> to start exporting:
/// <code>
/// {
///   "Telemetry": { "OtlpEndpoint": "http://localhost:4317" }
/// }
/// </code>
/// </example>
public static class TinkwellTelemetry
{
    /// <summary>
    /// Registers the OpenTelemetry SDK listening to the specified sources and
    /// meters. If <c>Telemetry:OtlpEndpoint</c> is set to a non-empty value
    /// in <paramref name="configuration"/>, the OTLP exporter is enabled for
    /// both traces and metrics.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder();
    /// builder.Services.AddTinkwellTelemetry(
    ///     builder.Configuration,
    ///     sourceNames: ["MyRunner.Traces"],
    ///     meterNames:  ["MyRunner.Metrics"]);
    /// </code>
    /// </example>
    public static IServiceCollection AddTinkwellTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string[] sourceNames,
        string[] meterNames)
    {
        var builder = services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                foreach (var source in sourceNames)
                    tracing.AddSource(source);
            })
            .WithMetrics(metrics =>
            {
                foreach (var meter in meterNames)
                    metrics.AddMeter(meter);
            });

        var endpoint = configuration["Telemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var uri = new Uri(endpoint);
            builder
                .WithTracing(tracing => tracing.AddOtlpExporter(o => o.Endpoint = uri))
                .WithMetrics(metrics => metrics.AddOtlpExporter(o => o.Endpoint = uri));
        }

        return services;
    }
}
