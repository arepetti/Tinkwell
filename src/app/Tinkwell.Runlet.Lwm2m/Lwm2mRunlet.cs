using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Expressions;
using Tinkwell.Lwm2m.Registration;
using Tinkwell.Runner;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// Headless runlet that provides an LwM2M server. Handles client
/// registration (/rd) and read/write operations on configured objects.
///
/// Object-to-measure mappings are declared in <c>lwm2m</c> blocks in
/// the <c>.tw</c> configuration file.
/// </summary>
public sealed class Lwm2mRunlet : IRunlet
{
    public void ConfigureServices(IServiceCollection services, IConfiguration settings)
    {
        var configPath = settings["path"];
        services.AddSingleton(new Lwm2mRunletOptions(configPath));
        services.AddSingleton<IExpressionEvaluator>(new ExpressionEvaluator());
        services.AddSingleton<RegistrationDirectory>();
        services.AddSingleton<ResourceStore>();
        services.AddHostedService<Lwm2mServerManager>();
    }

    public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
}

internal sealed record Lwm2mRunletOptions(string? ConfigPath);
