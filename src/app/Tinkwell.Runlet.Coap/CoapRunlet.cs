using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Expressions;
using Tinkwell.Health;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Headless runlet that starts one or more CoAP UDP servers and dispatches
/// requests through a pluggable binding chain. Resources and bindings are
/// declared in <c>coap</c> blocks in the <c>.tw</c> configuration file.
/// </summary>
public sealed class CoapRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new CoapRunletOptions(configPath));
        services.AddSingleton<IExpressionEvaluator>(new ExpressionEvaluator());

        var dropCheck = new IngestionDropCheck("coap-drops");
        services.AddSingleton(dropCheck);
        services.AddSingleton<IHealthCheck>(dropCheck);

        services.AddHostedService<CoapServerManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record CoapRunletOptions(string? ConfigPath);
