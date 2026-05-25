using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.I2c;

/// <summary>
/// Headless runlet that polls I2C devices on a Linux host and feeds raw
/// register values into Tinkwell measures. Config is declared in <c>i2c</c>
/// blocks in the <c>.tw</c> file.
/// </summary>
/// <remarks>
/// <para>Requires Linux with <c>/dev/i2c-*</c> bus access. Not supported on
/// Windows or macOS. Intended for quick tests and examples on single-board
/// computers (Raspberry Pi, BeagleBone, etc.), not for production use.</para>
/// </remarks>
public sealed class I2cRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new I2cRunletOptions(configPath));
        services.AddHostedService<I2cPollingManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record I2cRunletOptions(string? ConfigPath);
