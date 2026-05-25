using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

public sealed class MeasureHistoryRunletTests
{
    private static MeasureHistoryRunlet NewRunlet() => new();

    [Fact]
    public void ConfigureServices_stores_backend_assembly_name_as_is()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["backend"] = "Tinkwell.Measures.History.TimescaleDb",
            })
            .Build();

        var services = new ServiceCollection();
        NewRunlet().ConfigureServices(services, config);

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<MeasureHistoryOptions>();
        Assert.Equal("Tinkwell.Measures.History.TimescaleDb", opts.Backend);
    }

    [Fact]
    public void ConfigureServices_trims_whitespace_from_backend()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["backend"] = "  Acme.MyStore  ",
            })
            .Build();

        var services = new ServiceCollection();
        NewRunlet().ConfigureServices(services, config);

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<MeasureHistoryOptions>();
        Assert.Equal("Acme.MyStore", opts.Backend);
    }

    [Fact]
    public void ConfigureServices_missing_backend_throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            NewRunlet().ConfigureServices(new ServiceCollection(), config));
    }

    [Fact]
    public void ConfigureServices_batch_size_below_one_throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["backend"] = "Tinkwell.Measures.History.TimescaleDb",
                ["batch-size"] = "0",
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NewRunlet().ConfigureServices(new ServiceCollection(), config));

        Assert.Contains("batch-size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigureServices_flush_interval_ms_below_one_throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["backend"] = "Tinkwell.Measures.History.TimescaleDb",
                ["flush-interval-ms"] = "0",
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NewRunlet().ConfigureServices(new ServiceCollection(), config));

        Assert.Contains("flush-interval-ms", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
