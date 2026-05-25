using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runlet.Events;

namespace Tinkwell.Runlet.Events.Tests;

public class EventsRunletTests
{
    private static IConfiguration BuildConfig(IEnumerable<KeyValuePair<string, string?>>? keyValues = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(keyValues ?? Array.Empty<KeyValuePair<string, string?>>())
            .Build();
    }

    [Fact]
    public void ConfigureServices_Defaults_SubscriberChannelCapacity1000_DropWrite()
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        runlet.ConfigureServices(services, BuildConfig());
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(1000, cfg.SubscriberChannelConfig.Capacity);
        Assert.Equal(BoundedChannelFullMode.DropWrite, cfg.SubscriberChannelConfig.FullMode);
    }

    [Theory]
    [InlineData("500", 500)]
    [InlineData("1", 1)]
    [InlineData("2147483647", 2147483647)]
    public void ConfigureServices_ValidCapacity_Parsed(string value, int expected)
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var configuration = BuildConfig(
            [new("subscriber-channel-capacity", value)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(expected, cfg.SubscriberChannelConfig.Capacity);
        Assert.Equal(BoundedChannelFullMode.DropWrite, cfg.SubscriberChannelConfig.FullMode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void ConfigureServices_InvalidOrNonPositiveCapacity_UsesDefault1000(string? value)
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("subscriber-channel-capacity", value),
        };
        var configuration = BuildConfig(pairs);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(1000, cfg.SubscriberChannelConfig.Capacity);
    }

    [Fact]
    public void ConfigureServices_SubscriberChannelFullMode_ParsesDropOldest()
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var configuration = BuildConfig(
            [new("subscriber-channel-full-mode", "DropOldest")]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(BoundedChannelFullMode.DropOldest, cfg.SubscriberChannelConfig.FullMode);
    }

    [Fact]
    public void ConfigureServices_SubscriberChannelFullMode_IsCaseInsensitive()
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var configuration = BuildConfig(
            [new("subscriber-channel-full-mode", "dropoldest")]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(BoundedChannelFullMode.DropOldest, cfg.SubscriberChannelConfig.FullMode);
    }

    [Theory]
    [InlineData("DropWrite")]
    [InlineData("dropwrite")]
    public void ConfigureServices_SubscriberChannelFullMode_ParsesDropWrite(string mode)
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var configuration = BuildConfig(
            [new("subscriber-channel-full-mode", mode)]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(BoundedChannelFullMode.DropWrite, cfg.SubscriberChannelConfig.FullMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-enum")]
    public void ConfigureServices_InvalidFullMode_UsesDefaultDropWrite(string? mode)
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("subscriber-channel-full-mode", mode),
        };
        var configuration = BuildConfig(pairs);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(BoundedChannelFullMode.DropWrite, cfg.SubscriberChannelConfig.FullMode);
    }

    [Fact]
    public void ConfigureServices_CapacityAndFullMode_CanBeCombined()
    {
        var services = new ServiceCollection();
        var runlet = new EventsRunlet();
        var configuration = BuildConfig(
        [
            new("subscriber-channel-capacity", "42"),
            new("subscriber-channel-full-mode", "Wait"),
        ]);
        runlet.ConfigureServices(services, configuration);
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<EventFanOutConfig>();

        Assert.Equal(42, cfg.SubscriberChannelConfig.Capacity);
        Assert.Equal(BoundedChannelFullMode.Wait, cfg.SubscriberChannelConfig.FullMode);
    }

    [Fact]
    public void ConfigureServices_RegistersEventFanOutSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<EventFanOut>>(NullLogger<EventFanOut>.Instance);
        var runlet = new EventsRunlet();
        runlet.ConfigureServices(services, BuildConfig());
        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<EventFanOut>();
        var b = sp.GetRequiredService<EventFanOut>();
        Assert.Same(a, b);
    }
}
