using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Configuration;
using Tinkwell.Runlet.ProtobufGateway.Configuration;
using Tinkwell.Runner.Hosting;
using CoapServerLib = Tinkwell.Coap.Server.CoapServer;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Loads protobuf-gateway configuration, filters profiles targeting this
/// runlet, creates a single <see cref="CoapServerLib"/>, and registers
/// one route per distinct match pattern. Discovers and wires
/// <see cref="IGatewayMiddleware"/> implementations from DI.
/// </summary>
internal sealed class ProtobufGatewayWorker : BackgroundService
{
    private readonly ProtobufGatewayOptions _options;
    private readonly ServiceCache _cache;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IServiceProvider _services;
    private readonly Health.IngestionDropCheck _dropCheck;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ProtobufGatewayWorker> _logger;

    public ProtobufGatewayWorker(
        ProtobufGatewayOptions options,
        ServiceCache cache,
        CoordinatorPipeClient pipeClient,
        IServiceProvider services,
        Health.IngestionDropCheck dropCheck,
        ILoggerFactory loggerFactory,
        ILogger<ProtobufGatewayWorker> logger)
    {
        _options = options;
        _cache = cache;
        _pipeClient = pipeClient;
        _services = services;
        _dropCheck = dropCheck;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ProtobufGatewayConfig config;
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
            _logger.LogError(ex, "Failed to load protobuf-gateway configuration");
            return;
        }

        var profiles = FilterProfiles(config.Gateways);
        if (profiles.Count == 0)
        {
            _logger.LogInformation("No protobuf-gateway profiles match this runlet");
            return;
        }

        var server = new CoapServerLib(
            new CoapServerOptions
            {
                Port = _options.Port,
                Name = _options.RunletName ?? "protobuf-gateway",
                MaxConcurrentRequests = _options.MaxConcurrentRequests,
                MaxPendingRequests = _options.MaxPendingRequests,
            },
            _loggerFactory.CreateLogger<CoapServerLib>());

        var middlewares = _services.GetServices<IGatewayMiddleware>()
            .OrderBy(m => m.Order)
            .ToList();

        if (middlewares.Count > 0)
            _logger.LogInformation("Discovered {Count} gateway middleware(s)", middlewares.Count);

        RegisterRoutes(server, profiles, middlewares);

        _dropCheck.AddCounter(() => server.DroppedRequests);

        _logger.LogInformation(
            "Starting protobuf gateway on port {Port} with {Count} profile(s)",
            _options.Port, profiles.Count);

        try
        {
            await server.RunAsync(stoppingToken);
        }
        finally
        {
            await _cache.DisposeAsync();
        }
    }

    private void RegisterRoutes(
        CoapServerLib server,
        IReadOnlyList<GatewayProfileConfig> profiles,
        IReadOnlyList<IGatewayMiddleware> middlewares)
    {
        var grouped = profiles
            .GroupBy(p => p.MatchPattern, StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var matchPattern = group.Key;
            var allRules = group.SelectMany(p => p.AllowRules).ToList();
            var whitelist = new ServiceWhitelist(allRules);
            var template = new PathTemplate(matchPattern);
            var profileName = string.Join(", ", group.Select(p => p.Name));

            if (group.Count() > 1)
            {
                _logger.LogWarning(
                    "Profiles [{Names}] share match pattern '{Pattern}'; " +
                    "allow rules merged (indistinguishable until identity is implemented)",
                    profileName, matchPattern);
            }

            var handler = new GrpcTunnelHandler(
                template, whitelist, _cache, middlewares, profileName,
                _loggerFactory.CreateLogger<GrpcTunnelHandler>());

            server.MapPost(template.RoutePattern, (req, ct) => handler.HandleAsync(req, ct));

            _logger.LogDebug(
                "Registered route '{Route}' for profiles: {Names}",
                template.RoutePattern, profileName);
        }
    }

    private List<GatewayProfileConfig> FilterProfiles(
        IReadOnlyList<GatewayProfileConfig> all)
    {
        var name = _options.RunletName;

        return all
            .Where(p =>
                string.Equals(p.Target, "*", StringComparison.Ordinal) ||
                (name is not null && string.Equals(p.Target, name, StringComparison.Ordinal)))
            .ToList();
    }

    private async Task<ProtobufGatewayConfig> LoadConfigAsync(CancellationToken ct)
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

        _logger.LogDebug("Loading protobuf-gateway config from: {Path}", configPath);
        var parser = new ProtobufGatewayParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "Protobuf-gateway config loaded: {Count} profile(s) from {Path}",
            config.Gateways.Count, configPath);
        return config;
    }
}