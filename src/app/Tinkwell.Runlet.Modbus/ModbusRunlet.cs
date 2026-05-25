using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Modbus;

/// <summary>
/// Headless runlet that polls Modbus RTU/TCP devices and feeds register
/// values into Tinkwell measures. Config is declared in <c>modbus</c> blocks
/// in the <c>.tw</c> file.
/// </summary>
public sealed class ModbusRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new ModbusRunletOptions(configPath));
        services.AddHostedService<ModbusPollingManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record ModbusRunletOptions(string? ConfigPath);
