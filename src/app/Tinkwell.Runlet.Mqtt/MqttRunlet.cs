using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Expressions;
using Tinkwell.Health;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Headless runlet that connects to one or more MQTT brokers and runs a
/// binding chain per subscription (same model as CoAP). Each <c>subscribe</c>
/// must contain <c>on message { bind ... }</c>; bindings (e.g. event, measure, store)
/// perform side-effects. Config is declared in <c>mqtt</c> blocks in the <c>.tw</c> file.
/// </summary>
/// <remarks>
/// Settings:
/// <list type="bullet">
///   <item><c>path</c> — Path to the <c>.tw</c> file containing
///     <c>mqtt</c> blocks. Defaults to the coordinator config.</item>
/// </list>
/// </remarks>
public sealed class MqttRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new MqttRunletOptions(configPath));
        services.AddSingleton<IExpressionEvaluator>(new ExpressionEvaluator());

        var dropCheck = new IngestionDropCheck("mqtt-drops");
        services.AddSingleton(dropCheck);
        services.AddSingleton<IHealthCheck>(dropCheck);

        services.AddHostedService<MqttConnectionManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record MqttRunletOptions(string? ConfigPath);
