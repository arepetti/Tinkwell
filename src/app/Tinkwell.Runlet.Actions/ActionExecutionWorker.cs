using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Actions;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Runner.Hosting;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Actions;

/// <summary>
/// Background service that loads action configuration, discovers external
/// handler assemblies, subscribes to the event bus and executes matching
/// action handlers for each incoming event.
/// </summary>
internal sealed class ActionExecutionWorker : BackgroundService
{
    private readonly ActionsRunletOptions _options;
    private readonly CoordinatorPipeClient _pipeClient;
    private readonly IServiceProvider _services;
    private readonly IServiceDiscovery _discovery;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IEnumerable<IActionHandler> _builtInHandlers;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ActionExecutionWorker> _logger;

    private readonly HashSet<string> _disabledHandlers = new(StringComparer.OrdinalIgnoreCase);

    public ActionExecutionWorker(
        ActionsRunletOptions options,
        CoordinatorPipeClient pipeClient,
        IServiceProvider services,
        IServiceDiscovery discovery,
        IExpressionEvaluator evaluator,
        IEnumerable<IActionHandler> builtInHandlers,
        IHostApplicationLifetime lifetime,
        ILogger<ActionExecutionWorker> logger)
    {
        _options = options;
        _pipeClient = pipeClient;
        _services = services;
        _discovery = discovery;
        _evaluator = evaluator;
        _builtInHandlers = builtInHandlers;
        _lifetime = lifetime;
        _logger = logger;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "External handler assemblies are deployed alongside the runner.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ActionsConfig config;
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
            _logger.LogError(ex, "Failed to load actions configuration");
            return;
        }

        if (config.Actions.Count == 0)
        {
            _logger.LogInformation("No actions defined — worker idle");
            return;
        }

        var pluginResolver = _services.GetService(typeof(PluginResolver)) as PluginResolver;
        var externalHandlers = ActionHandlerLoader.LoadExternalHandlers(config, _services, _logger, pluginResolver);
        var handlerMap = new Dictionary<string, IActionHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in _builtInHandlers)
            handlerMap[h.Name] = h;
        foreach (var h in externalHandlers)
            handlerMap[h.Name] = h;

        _logger.LogInformation(
            "Action execution started: {ActionCount} action(s), {HandlerCount} handler(s) available ({ExternalCount} external)",
            config.Actions.Count, handlerMap.Count, externalHandlers.Count);

        EventsGrpc.EventBus.EventBusClient? client = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                client ??= await DiscoverEventBusAsync(stoppingToken);
                if (client is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                using var stream = client.Subscribe(
                    new EventsGrpc.SubscribeRequest(),
                    cancellationToken: stoppingToken);

                await foreach (var msg in stream.ResponseStream.ReadAllAsync(stoppingToken))
                {
                    var envelope = ToEnvelope(msg);
                    await ProcessEventAsync(envelope, config, handlerMap, client, stoppingToken);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger.LogWarning("Event bus unavailable, retrying in 5s");
                client = null;
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action execution error, retrying in 5s");
                client = null;
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessEventAsync(
        EventEnvelope envelope,
        ActionsConfig config,
        Dictionary<string, IActionHandler> handlerMap,
        EventsGrpc.EventBus.EventBusClient? eventBusClient,
        CancellationToken ct)
    {
        foreach (var action in config.Actions)
        {
            if (!MatchesFilters(action, envelope))
                continue;

            foreach (var handlerDef in action.Handlers)
            {
                if (_disabledHandlers.Contains(handlerDef.HandlerName))
                    continue;

                if (!handlerMap.TryGetValue(handlerDef.HandlerName, out var handler))
                {
                    _logger.LogWarning(
                        "Handler '{Handler}' not found for action '{Action}'",
                        handlerDef.HandlerName, action.Name);
                    continue;
                }

                var policy = handlerDef.OnError ?? action.OnError;
                var maxAttempts = 1 + (policy?.Retry?.Count ?? 0);
                Exception? lastEx = null;

                for (int attempt=0; attempt < maxAttempts; ++attempt)
                {
                    try
                    {
                        await handler.ExecuteAsync(envelope, handlerDef.Parameters, _evaluator, ct);
                        lastEx = null;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        if (attempt < maxAttempts - 1)
                        {
                            var retry = policy!.Retry!;
                            var delay = (int)(retry.DelayMs * Math.Pow(retry.BackoffMultiplier, attempt));
                            _logger.LogWarning(ex,
                                "Handler '{Handler}' failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                                handlerDef.HandlerName, attempt + 1, retry.Count, delay);
                            await Task.Delay(delay, ct);
                        }
                    }
                }

                if (lastEx is not null)
                {
                    await DispatchErrorPolicyAsync(
                        policy, lastEx, handlerDef.HandlerName, action.Name,
                        envelope, eventBusClient, ct);
                }
            }
        }
    }

    private async Task DispatchErrorPolicyAsync(
        ErrorPolicy? policy,
        Exception ex,
        string handlerName,
        string actionName,
        EventEnvelope envelope,
        EventsGrpc.EventBus.EventBusClient? eventBusClient,
        CancellationToken ct)
    {
        var action = policy?.Action ?? ErrorPolicyAction.ResumeNext;

        switch (action)
        {
            case ErrorPolicyAction.ResumeNext:
                _logger.LogWarning(ex,
                    "Handler '{Handler}' failed for action '{Action}' (event: {Source}.{Name}), resuming",
                    handlerName, actionName, envelope.Source, envelope.Name);
                break;

            case ErrorPolicyAction.StopThis:
                _logger.LogError(ex,
                    "Handler '{Handler}' failed for action '{Action}', disabling handler",
                    handlerName, actionName);
                _disabledHandlers.Add(handlerName);
                break;

            case ErrorPolicyAction.StopApplication:
                _logger.LogCritical(ex,
                    "Handler '{Handler}' failed for action '{Action}', stopping application",
                    handlerName, actionName);
                _lifetime.StopApplication();
                break;

            case ErrorPolicyAction.Publish when eventBusClient is not null:
                _logger.LogWarning(ex,
                    "Handler '{Handler}' failed for action '{Action}', publishing failure event '{Event}'",
                    handlerName, actionName, policy!.EventName);
                try
                {
                    var request = new EventsGrpc.PublishEventRequest
                    {
                        Source = "actions",
                        Verb = EventsGrpc.EventVerb.Failed,
                        Name = policy.EventName ?? "error",
                        Object = handlerName,
                        Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    };
                    request.Payload["_error_message"] = ex.Message;
                    request.Payload["_error_type"] = ex.GetType().Name;
                    request.Payload["_handler"] = handlerName;
                    request.Payload["_action"] = actionName;
                    request.Payload["_event_source"] = envelope.Source ?? "";
                    request.Payload["_event_name"] = envelope.Name ?? "";
                    if (policy.EventProperties is not null)
                    {
                        foreach (var (key, val) in policy.EventProperties)
                            request.Payload[key] = val.ToString() ?? "";
                    }
                    await eventBusClient.PublishAsync(request, cancellationToken: ct);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception pubEx)
                {
                    _logger.LogWarning(pubEx, "Failed to publish error event");
                }
                break;

            default:
                _logger.LogWarning(ex,
                    "Handler '{Handler}' failed for action '{Action}', resuming (publish unavailable)",
                    handlerName, actionName);
                break;
        }
    }

    internal static bool MatchesFilters(ActionDefinition action, EventEnvelope envelope)
    {
        if (action.NameFilter is not null &&
            !string.Equals(action.NameFilter, envelope.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (action.SourceFilter is not null &&
            !string.Equals(action.SourceFilter, envelope.Source, StringComparison.OrdinalIgnoreCase))
            return false;

        if (action.VerbFilter is not null)
        {
            var verbName = envelope.Verb == EventVerb.Custom
                ? envelope.CustomVerb ?? string.Empty
                : envelope.Verb.ToString();

            if (!string.Equals(action.VerbFilter, verbName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private async Task<EventsGrpc.EventBus.EventBusClient?> DiscoverEventBusAsync(CancellationToken ct)
    {
        try
        {
            var svc = await _discovery.DiscoverAsync("events", ct);

            if (svc is null)
            {
                _logger.LogWarning("Event bus not found — will retry");
                return null;
            }

            var client = await _discovery.CreateInstanceAsync<EventsGrpc.EventBus.EventBusClient>(svc, ct);
            _logger.LogDebug("Event bus discovered at {Url}", svc.Url);
            return client;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover event bus");
            return null;
        }
    }

    private static EventEnvelope ToEnvelope(EventsGrpc.EventMessage msg) => new()
    {
        Source = msg.Source,
        Verb = (EventVerb)(int)msg.Verb,
        CustomVerb = string.IsNullOrEmpty(msg.CustomVerb) ? null : msg.CustomVerb,
        Name = msg.Name,
        Object = string.IsNullOrEmpty(msg.Object) ? null : msg.Object,
        CorrelationId = string.IsNullOrEmpty(msg.CorrelationId) ? null : msg.CorrelationId,
        Timestamp = msg.Timestamp?.ToDateTime() ?? DateTime.UtcNow,
        Payload = msg.Payload.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
    };

    private async Task<ActionsConfig> LoadConfigAsync(CancellationToken ct)
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

        _logger.LogDebug("Loading actions from: {Path}", configPath);
        var parser = new ActionsParser(logger: _logger);
        var config = await parser.LoadFileAsync(configPath);
        _logger.LogInformation(
            "Actions config loaded: {Count} action(s) from {Path}",
            config.Actions.Count, configPath);
        return config;
    }
}