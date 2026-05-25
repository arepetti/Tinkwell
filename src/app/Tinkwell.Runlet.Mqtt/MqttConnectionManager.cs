using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Mqtt.Configuration;
using Tinkwell.Expressions;
using Tinkwell.Health;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Loads MQTT configuration, resolves bindings, and starts one
/// <see cref="MqttConnectionWorker"/> per MQTT connection definition.
/// </summary>
internal sealed class MqttConnectionManager : BackgroundService
{
    private readonly MqttRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IServiceProvider _services;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly IngestionDropCheck _dropCheck;

    public MqttConnectionManager(
        MqttRunletOptions options,
        CoordinatorPipeClient pipeClient,
        IServiceProvider services,
        IExpressionEvaluator evaluator,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory,
        ILogger<MqttConnectionManager> logger,
        IngestionDropCheck dropCheck)
    {
        _options = options;
        _pipeClient = pipeClient;
        _services = services;
        _evaluator = evaluator;
        _lifetime = lifetime;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _dropCheck = dropCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MqttConfig config;
        try
        {
            config = await LoadConfigAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load MQTT configuration");
            return;
        }

        if (config.Connections.Count == 0)
        {
            _logger.LogInformation("No MQTT connections defined — worker idle");
            return;
        }

        var assemblyNames = config.Connections
            .SelectMany(c => c.Subscriptions)
            .SelectMany(s => s.VerbBlocks)
            .SelectMany(v => v.Bindings)
            .Select(b => b.AssemblyName ?? Tinkwell.Integration.IntegrationBindingDefaults.DefaultAssembly)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var pluginResolver = _services.GetService<PluginResolver>();
        var bindings = BindingLoader.LoadBindings(
            assemblyNames, _services, _logger, pluginResolver);

        _logger.LogInformation("Loaded {Count} integration binding(s): {Names}",
            bindings.Count, string.Join(", ", bindings.Keys));

        var executor = new MqttBindingChainExecutor(
            bindings, _evaluator, _lifetime,
            _loggerFactory.CreateLogger<MqttBindingChainExecutor>());

        var middlewares = _services.GetServices<IMqttMiddleware>()
            .OrderBy(m => m.Order)
            .ToList();

        if (middlewares.Count > 0)
            _logger.LogInformation("Discovered {Count} MQTT middleware(s)", middlewares.Count);

        _logger.LogInformation("Starting {Count} MQTT connection(s)", config.Connections.Count);

        var workers = config.Connections.Select(conn =>
        {
            var workerLogger = _loggerFactory.CreateLogger<MqttConnectionWorker>();
            var worker = new MqttConnectionWorker(conn, executor, middlewares, _evaluator, workerLogger);
            _dropCheck.AddCounter(() => worker.DroppedMessages);
            return worker.RunAsync(stoppingToken);
        }).ToArray();

        await Task.WhenAll(workers);
    }

    private async Task<MqttConfig> LoadConfigAsync(CancellationToken ct)
    {
        string configPath;
        if (!string.IsNullOrWhiteSpace(_options.ConfigPath))
        {
            configPath = _options.ConfigPath;
        }
        else
        {
            _logger.LogDebug("No path configured, fetching coordinator config path");
            configPath = await _pipeClient.FetchConfigPathAsync(ct);
        }

        _logger.LogDebug("Loading MQTT config from: {Path}", configPath);
        var parser = new MqttConfigParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "MQTT config loaded: {Count} connection(s) from {Path}",
            config.Connections.Count, configPath);
        return config;
    }
}