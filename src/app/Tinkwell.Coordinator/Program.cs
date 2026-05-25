using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator.Pipes;
using Tinkwell.Coordinator.ProcessManagement;
using Tinkwell.Logging;
using Tinkwell.Pipes;
using Tinkwell.Telemetry;

namespace Tinkwell.Coordinator;

public static class Program
{
    private static ConfigPathInfo? _configPathInfo;

    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddTinkwellConsole();
        
        var config = await LoadConfigAsync(args);
        if (config is null)
            return 1;

        builder.Services.Configure<CoordinatorOptions>(
            builder.Configuration.GetSection("Coordinator"));
        builder.Services.Configure<RestartPolicyOptions>(
            builder.Configuration.GetSection("Coordinator:RestartPolicy"));
        builder.Services.Configure<EndpointOptions>(
            builder.Configuration.GetSection("Coordinator:Endpoints"));

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(_configPathInfo!);
        builder.Services.AddSingleton(new RunnerRegistry(config));
        builder.Services.Configure<PipeServerOptions>(
            builder.Configuration.GetSection("Coordinator:PipeServer"));
        builder.Services.AddSingleton<EndpointAllocator>();
        builder.Services.AddSingleton<ServiceRegistry>();
        builder.Services.AddSingleton<PipeCommandDispatcher>();
        builder.Services.AddSingleton<RunnerProcessLauncher>();
        builder.Services.AddSingleton<RunnerMonitor>();
        builder.Services.AddTinkwellTelemetry(builder.Configuration,
            sourceNames: [OtTraces.SourceName],
            meterNames: [OtMetrics.MeterName]);
        builder.Services.AddHostedService<CoordinatorService>();

        var host = builder.Build();
        await host.RunAsync();

        return 0;
    }

    private static async Task<EnsembleConfig?> LoadConfigAsync(string[] args)
    {
        var configPath = ResolveConfigPath(args);
        if (configPath is null)
        {
            Console.Error.WriteLine("Usage: Tinkwell.Coordinator <config-file.tw>");
            return null;
        }

        _configPathInfo = new ConfigPathInfo(Path.GetFullPath(configPath));

        var logger = LoggerFactory
            .Create(b => b.AddTinkwellConsole())
            .CreateLogger<CoordinatorService>();

        logger.LogInformation("Loading ensemble: {Path}", configPath);

        var parser = new EnsembleParser(logger: logger,
            options: new ParserOptions { Lax = true });
        EnsembleConfig config;

        try
        {
            config = await parser.LoadFileAsync(configPath);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to parse configuration file: {Path}", configPath);
            return null;
        }

        logger.LogInformation("Loaded ensemble with {Count} runner(s) from {Path}",
            config.Runners.Count, configPath);

        foreach (var runner in config.Runners)
        {
            logger.LogDebug("  Runner '{Name}' -> {Path} ({RunletCount} runlet(s))",
                runner.Name, runner.ExecutablePath, runner.Runlets.Count);

            foreach (var runlet in runner.Runlets)
            {
                logger.LogDebug("    Runlet '{Name}' -> {Path}",
                    runlet.Name, runlet.AssemblyPath);
            }
        }

        return config;
    }

    private static string? ResolveConfigPath(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (path is null)
            return "ensemble.tw";

        if (!path.EndsWith(".tw", StringComparison.OrdinalIgnoreCase))
            path += ".tw";

        return File.Exists(path) ? path : null;
    }
}