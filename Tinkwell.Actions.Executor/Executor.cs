using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Actions.Executor.Agents;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Services;

namespace Tinkwell.Actions.Executor;

sealed class Executor : IAsyncDisposable
{
    public Executor(IServiceProvider serviceProvider, ILogger<Executor> logger, ServiceLocator locator, IConfigFileReader<ITwaFile> fileReader, ExecutorOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _locator = locator;
        _fileReader = fileReader;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _eventsGateway = await _locator.FindEventsGatewayAsync(cancellationToken);

        _logger.LogDebug("Loading actions from {Path}", _options.Path);
        var file = await _fileReader.ReadAsync(_options.Path, cancellationToken);
        _listeners = file.Listeners.Select(CreateListener).ToList();

        await _dispatchWorker.StartAsync(DispatchIntentsAsync);
        await _listeningWorker.StartAsync(SubscribeToEventsAsync);
        _logger.LogDebug("Reactor started successfully, now watching for changes");
    }

    public async ValueTask DisposeAsync()
    {
        await _listeningWorker.StopAsync(CancellationToken.None);

        if (_locator is not null)
            await _locator.DisposeAsync();

        if (_eventsGateway is not null)
            await _eventsGateway.DisposeAsync();
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Executor> _logger;
    private readonly ServiceLocator _locator;
    private readonly IConfigFileReader<ITwaFile> _fileReader;
    private readonly ExecutorOptions _options;
    private List<Listener>? _listeners;
    private readonly CancellableLongRunningTask _listeningWorker = new();
    private readonly CancellableLongRunningTask _dispatchWorker = new();
    private GrpcService<EventsGateway.EventsGatewayClient>? _eventsGateway;
    private readonly BlockingCollection<Intent> _queue = new();

    private async Task SubscribeToEventsAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_listeners is not null);
        Debug.Assert(_eventsGateway is not null);

        var eventsToWatch = _listeners.Select(x => new SubscribeToMatchingManyEventsRequest.Types.Match
        {
            Topic = x.Topic,
            Subject = x.Subject ?? "",
            Verb = x.Verb ?? "",
            Object = x.Object ?? "",
            MatchId = x.Id
        }).ToList();

        var request = new SubscribeToMatchingManyEventsRequest
        {
            Matches = { eventsToWatch }
        };

        _logger.LogDebug("Subscribing to events for {Count} listeners", _listeners.Count);
        using var call = _eventsGateway.Client.SubscribeToMatchingMany(request, cancellationToken: cancellationToken);
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                var listener = _listeners!.First(x => x.Id == response.MatchId);
                if (!listener.Enabled)
                    continue;

                foreach (var directive in listener.Directives)
                    _queue.Add(new()
                    {
                        Listener = listener,
                        Event = new()
                        {
                            Id = response.Id,
                            CorrelationId = response.CorrelationId,
                            Topic = response.Topic,
                            Subject = response.Subject,
                            Verb = response.Verb.ToString(),
                            Object = response.Object,
                        },
                        Directive = directive,
                        Payload = response.Payload
                    });
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
        {
            // We're probably shutting down?
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during subscription: {Message}.", e.Message);
        }
    }

    private async Task DispatchIntentsAsync(CancellationToken cancellationToken)
    {
        var factory = new AgentFactory();
        foreach (var intent in _queue.GetConsumingEnumerable(cancellationToken))
        {
            try
            {
                IAgent? agent = null;
                try
                {
                    agent = factory.Create(_serviceProvider, intent);
                }
                catch (ExecutorException e)
                {
                    _logger.LogError(e, "Disabling listener because it failed to create agent for intent '{IntentId}': {Message}.", intent.Event.Id, e.Message);
                    intent.Listener.Enabled = false;
                }
                
                if (agent is not null)
                    await agent.ExecuteAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An unexpected error occurred while executing the intent '{IntentId}': {Message}.", intent.Event.Id, e.Message);
            }
        }
    }

    private Listener CreateListener(WhenDefinition definition)
    {
        return new Listener
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = definition.Topic,
            Subject = definition.Subject,
            Verb = definition.Verb,
            Object = definition.Object,
            Directives = definition.Actions.Select(CreateDirective).ToList()
        };
    }

    private Directive CreateDirective(ActionDefinition action)
    {
        var type = AgentsRecruiter.FindAgent(action.Name);
        if (type is not null)
            return new Directive { AgentType = type, Parameters = action.Properties };

        _logger.LogError("One listener requires a command '{Command}' but no such command is registered.", action.Name);
        return new Directive { AgentType = typeof(PassAgent), Parameters = new Dictionary<string, object>() };
    }
}
