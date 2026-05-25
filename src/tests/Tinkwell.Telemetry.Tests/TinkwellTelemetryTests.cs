using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Tinkwell.Telemetry.Tests;

public class TinkwellTelemetryTests
{
    private static IConfiguration BuildConfiguration(string? otlpEndpoint)
    {
        var data = new Dictionary<string, string?>();
        if (otlpEndpoint is not null)
            data["Telemetry:OtlpEndpoint"] = otlpEndpoint;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void AddTinkwellTelemetry_WithoutEndpoint_RegistersProvidersWithoutExporter()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(otlpEndpoint: null);

        services.AddTinkwellTelemetry(
            configuration,
            sourceNames: new[] { "Test.Source" },
            meterNames: new[] { "Test.Meter" });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddTinkwellTelemetry_WithEmptyEndpoint_NoExporter()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(otlpEndpoint: "   ");

        services.AddTinkwellTelemetry(
            configuration,
            sourceNames: Array.Empty<string>(),
            meterNames: Array.Empty<string>());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddTinkwellTelemetry_WithOtlpEndpoint_BuildsProviderSuccessfully()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(otlpEndpoint: "http://localhost:4317");

        services.AddTinkwellTelemetry(
            configuration,
            sourceNames: new[] { "Test.Source" },
            meterNames: new[] { "Test.Meter" });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddTinkwellTelemetry_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(otlpEndpoint: null);

        var result = services.AddTinkwellTelemetry(
            configuration,
            sourceNames: Array.Empty<string>(),
            meterNames: Array.Empty<string>());

        Assert.Same(services, result);
    }
}
