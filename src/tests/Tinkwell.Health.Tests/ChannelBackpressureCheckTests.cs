using Tinkwell.Health;

namespace Tinkwell.Health.Tests;

public sealed class ChannelBackpressureCheckTests
{
    [Fact]
    public async Task BeforeAttach_ReturnsHealthy()
    {
        var check = new ChannelBackpressureCheck("test", 100);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task BelowThreshold_ReturnsHealthy()
    {
        var check = new ChannelBackpressureCheck("test", 100, threshold: 0.8);
        check.Attach(() => 50);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task AtThreshold_ReturnsDegraded()
    {
        var check = new ChannelBackpressureCheck("test", 100, threshold: 0.8);
        check.Attach(() => 80);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("80%", result.Message);
        Assert.Contains("80/100", result.Message);
    }

    [Fact]
    public async Task AboveThreshold_ReturnsDegraded()
    {
        var check = new ChannelBackpressureCheck("test", 100, threshold: 0.8);
        check.Attach(() => 95);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task FullChannel_ReturnsDegraded()
    {
        var check = new ChannelBackpressureCheck("test", 256, threshold: 0.8);
        check.Attach(() => 256);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("100%", result.Message);
    }

    [Fact]
    public async Task EmptyChannel_ReturnsHealthy()
    {
        var check = new ChannelBackpressureCheck("test", 256);
        check.Attach(() => 0);

        var result = await check.CheckAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Name_MatchesConstructorArg()
    {
        var check = new ChannelBackpressureCheck("derived-measures", 256);
        Assert.Equal("derived-measures", check.Name);
    }
}
