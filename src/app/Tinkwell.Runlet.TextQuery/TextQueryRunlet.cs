using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.TextQuery;

/// <summary>
/// Headless runlet that polls text-based data sources (TCP, serial, file, command),
/// extracts values with regex, and feeds them into Tinkwell measures.
/// Config is declared in <c>query</c> blocks in the <c>.tw</c> file.
/// </summary>
public sealed class TextQueryRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new TextQueryRunletOptions(configPath));
        services.AddHostedService<TextQueryPollingManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record TextQueryRunletOptions(string? ConfigPath);
