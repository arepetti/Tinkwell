using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tinkwell.Runlet.EventPersistence;

namespace Tinkwell.Runlet.EventPersistence.Tests;

public class EventPersistenceRunletTests
{
    private static IConfiguration BuildConfig(IEnumerable<KeyValuePair<string, string?>>? keyValues = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(keyValues ?? Array.Empty<KeyValuePair<string, string?>>())
            .Build();
    }

    [Fact]
    public void ConfigureServices_Defaults_UsesDefaultDbPathBatchAndFlush()
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        runlet.ConfigureServices(services, BuildConfig());
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal("events.db", opts.DbPath);
        Assert.Equal(100, opts.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(1), opts.FlushInterval);
    }

    [Fact]
    public void ConfigureServices_RegistersEventPersistenceOptionsSingleton()
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        runlet.ConfigureServices(services, BuildConfig());

        var d = services.Single(s => s.ServiceType == typeof(EventPersistenceOptions));
        Assert.Equal(ServiceLifetime.Singleton, d.Lifetime);
    }

    [Fact]
    public void ConfigureServices_RegistersEventPersistenceWorkerAsHostedService()
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        runlet.ConfigureServices(services, BuildConfig());

        var hosted = services
            .Where(s => s.ServiceType == typeof(IHostedService) || s.ImplementationType == typeof(EventPersistenceWorker))
            .ToList();
        Assert.Contains(hosted, s =>
            s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(EventPersistenceWorker));
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("200000", 10_000)]
    [InlineData("1", 1)]
    [InlineData("10000", 10_000)]
    public void ConfigureServices_BatchSize_IsClampedToOneAndTenThousand(string value, int expected)
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("batch-size", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(expected, opts.BatchSize);
    }

    [Theory]
    [InlineData("0", 0.001)]
    [InlineData("0.00001", 0.001)]
    [InlineData("9999", 3600.0)]
    [InlineData("3600", 3600.0)]
    [InlineData("0.5", 0.5)]
    public void ConfigureServices_FlushIntervalSeconds_IsClampedToRange(string value, double expectedSeconds)
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("flush-interval", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        var expectedSpan = TimeSpan.FromSeconds(expectedSeconds);
        Assert.Equal(expectedSpan, opts.FlushInterval);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ConfigureServices_FlushInterval_NonFinite_FallsBackToDefaultOneSecond(string value)
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("flush-interval", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(TimeSpan.FromSeconds(1), opts.FlushInterval);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void ConfigureServices_FlushInterval_Unparseable_FallsBackToDefaultOneSecond(string value)
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("flush-interval", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(TimeSpan.FromSeconds(1), opts.FlushInterval);
    }

    [Theory]
    [InlineData("totally-bad", 100)]
    [InlineData("", 100)]
    public void ConfigureServices_BatchSize_Unparseable_UsesDefaultOneHundred(string value, int expected)
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("batch-size", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(expected, opts.BatchSize);
    }

    [Fact]
    public void ConfigureServices_DbPath_ExplicitValue_IsUsed()
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        const string path = @"C:\data\ep\custom-events.db";
        var configuration = BuildConfig(
            [new("db-path", path)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(path, opts.DbPath);
    }

    [Fact]
    public void ConfigureServices_DbPath_EmptyString_IsPreserved()
    {
        var services = new ServiceCollection();
        var runlet = new EventPersistenceRunlet();
        var configuration = BuildConfig(
            [new("db-path", "")]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<EventPersistenceOptions>();

        Assert.Equal(string.Empty, opts.DbPath);
    }
}
