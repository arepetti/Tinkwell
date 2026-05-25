using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Lwm2m.Configuration;
using Tinkwell.Integration;
using Tinkwell.Lwm2m.Registration;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// Loads LwM2M configuration and launches one
/// <see cref="Lwm2mServerWorker"/> per configured server.
/// </summary>
internal sealed class Lwm2mServerManager : BackgroundService
{
    private readonly Lwm2mRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IServiceProvider _services;
    private readonly RegistrationDirectory _registrationDirectory;
    private readonly ResourceStore _resourceStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Lwm2mServerManager> _logger;

    public Lwm2mServerManager(
        Lwm2mRunletOptions options,
        CoordinatorPipeClient pipeClient,
        IServiceProvider services,
        RegistrationDirectory registrationDirectory,
        ResourceStore resourceStore,
        ILoggerFactory loggerFactory,
        ILogger<Lwm2mServerManager> logger)
    {
        _options = options;
        _pipeClient = pipeClient;
        _services = services;
        _registrationDirectory = registrationDirectory;
        _resourceStore = resourceStore;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Lwm2mConfig config;
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
            _logger.LogError(ex, "Failed to load LwM2M configuration");
            return;
        }

        if (config.Servers.Count == 0)
        {
            _logger.LogInformation("No LwM2M servers configured");
            return;
        }

        var codeResources = _services.GetServices<ILwm2mResourceProvider>()
            .SelectMany(p => p.GetResources())
            .ToList();

        if (codeResources.Count > 0)
            _logger.LogInformation("Discovered {Count} code-defined LwM2M resource(s)", codeResources.Count);

        var tasks = new List<Task>();

        foreach (var server in config.Servers)
        {
            var serverLogger = _loggerFactory.CreateLogger($"LwM2M.{server.Name}");

            var dispatcher = new Lwm2mRequestDispatcher(
                server, _registrationDirectory, _resourceStore, codeResources, serverLogger);

            var worker = new Lwm2mServerWorker(server, dispatcher, serverLogger);

            var purger = new RegistrationPurger(
                _registrationDirectory, serverLogger);

            tasks.Add(worker.StartAsync(stoppingToken));
            tasks.Add(purger.StartAsync(stoppingToken));
        }

        _logger.LogInformation("Started {Count} LwM2M server(s)", config.Servers.Count);
        await Task.WhenAll(tasks);
    }

    private async Task<Lwm2mConfig> LoadConfigAsync(CancellationToken ct)
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

        _logger.LogDebug("Loading LwM2M config from: {Path}", configPath);
        var parser = new Lwm2mConfigParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "LwM2M config loaded: {Count} server(s) from {Path}",
            config.Servers.Count, configPath);
        return config;
    }
}