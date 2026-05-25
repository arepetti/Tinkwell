using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Coap.Configuration;
using Tinkwell.Expressions;
using Tinkwell.Health;
using Tinkwell.Integration;
using Tinkwell.Runner.Hosting;
using Tinkwell.Runlet.Coap.Observe;
using CoapServerLib = Tinkwell.Coap.Server.CoapServer;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Loads CoAP configuration, resolves bindings, and launches one
/// <see cref="CoapServerLib"/> per configured server.
/// </summary>
internal sealed class CoapServerManager : BackgroundService
{
    private readonly CoapRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IServiceProvider _services;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CoapServerManager> _logger;
    private readonly IngestionDropCheck _dropCheck;

    public CoapServerManager(
        CoapRunletOptions options,
        CoordinatorPipeClient pipeClient,
        IServiceProvider services,
        IExpressionEvaluator evaluator,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory,
        ILogger<CoapServerManager> logger,
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
        CoapConfig config;
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
            _logger.LogError(ex, "Failed to load CoAP configuration");
            return;
        }

        if (config.Servers.Count == 0)
        {
            _logger.LogInformation("No CoAP servers configured");
            return;
        }

        var assemblyNames = config.Servers
            .SelectMany(s => s.Resources)
            .SelectMany(r => r.VerbBlocks)
            .SelectMany(v => v.Bindings)
            .Select(b => b.AssemblyName ?? IntegrationBindingDefaults.DefaultAssembly)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var pluginResolver = _services.GetService<PluginResolver>();
        var bindings = BindingLoader.LoadBindings(
            assemblyNames, _services, _logger, pluginResolver);

        _logger.LogInformation("Loaded {Count} integration binding(s): {Names}",
            bindings.Count, string.Join(", ", bindings.Keys));

        var bindingProviders = _services.GetServices<ICoapBindingProvider>().ToList();
        var middlewares = _services.GetServices<ICoapRequestMiddleware>()
            .OrderBy(m => m.Order)
            .ToList();

        if (bindingProviders.Count > 0)
            _logger.LogInformation("Discovered {Count} CoAP binding provider(s)", bindingProviders.Count);
        if (middlewares.Count > 0)
            _logger.LogInformation("Discovered {Count} CoAP middleware(s)", middlewares.Count);

        var tasks = new List<Task>();
        foreach (var server in config.Servers)
        {
            var serverLogger = _loggerFactory.CreateLogger($"CoAP.{server.Name}");
            var changeNotifier = new ResourceChangeNotifier(serverLogger);

            var executor = new BindingChainExecutor(
                bindings, _evaluator, _lifetime, serverLogger);

            var coapServer = new CoapServerLib(
                new CoapServerOptions
                {
                    Port = server.Port,
                    Name = server.Name,
                    MaxConcurrentRequests = server.MaxConcurrentRequests,
                    MaxPendingRequests = server.MaxPendingRequests,
                },
                _loggerFactory.CreateLogger<CoapServerLib>());

            foreach (var resource in server.Resources)
            {
                var handler = new TinkwellCoapHandler(
                    server, resource, executor, serverLogger);
                coapServer.Map(resource.PathPattern, handler);
            }

            var routeBuilder = new CoapRouteBuilderAdapter();
            foreach (var provider in bindingProviders)
                provider.Configure(routeBuilder);
            routeBuilder.ApplyTo(coapServer, middlewares, serverLogger);

            _dropCheck.AddCounter(() => coapServer.DroppedRequests);

            tasks.Add(BridgeNotificationsAsync(changeNotifier, coapServer, stoppingToken));
            tasks.Add(coapServer.RunAsync(stoppingToken));
        }

        _logger.LogInformation("Started {Count} CoAP server(s)", config.Servers.Count);

        await Task.WhenAll(tasks);
    }

    private static async Task BridgeNotificationsAsync(
        ResourceChangeNotifier notifier, CoapServerLib server, CancellationToken ct)
    {
        try
        {
            await foreach (var path in notifier.Reader.ReadAllAsync(ct))
                server.NotifyObservers(path);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<CoapConfig> LoadConfigAsync(CancellationToken ct)
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

        _logger.LogDebug("Loading CoAP config from: {Path}", configPath);
        var parser = new CoapConfigParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "CoAP config loaded: {Count} server(s) from {Path}",
            config.Servers.Count, configPath);
        return config;
    }
}