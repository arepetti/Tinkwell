using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Actions.Executor.Agents;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Services;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Actions.Executor;

sealed class Executor : IAsyncDisposable
{
    public Executor(ILogger<Executor> logger, IEventsGateway eventsGateway, IConfigFileReader<ITwaFile> fileReader, IIntentDispatcher dispatcher, ExecutorOptions options)
    {
        _logger = logger;
        _eventsGateway = eventsGateway;
        _fileReader = fileReader;
        _dispatcher = dispatcher;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = HostingInformation.GetFullPath(_options.Path);
        _logger.LogDebug("Loading actions from {Path}", path);
        var file = await _fileReader.ReadAsync(path, cancellationToken);
        _listeners = file.Listeners.Select(CreateListener).ToList();

        await _dispatchWorker.StartAsync(DispatchIntentsAsync);
        await _listeningWorker.StartAsync(SubscribeToEventsAsync);
        _logger.LogInformation("Reactor started successfully");
    }

    public async ValueTask DisposeAsync()
    {
        await _listeningWorker.StopAsync(CancellationToken.None);
        await _dispatchWorker.StopAsync(CancellationToken.None);
    }

    private readonly ILogger<Executor> _logger;
    private readonly IConfigFileReader<ITwaFile> _fileReader;
    private readonly IIntentDispatcher _dispatcher;
    private readonly ExecutorOptions _options;
    private List<Listener>? _listeners;
    private readonly CancellableLongRunningTask _listeningWorker = new();
    private readonly CancellableLongRunningTask _dispatchWorker = new();
    private readonly IEventsGateway _eventsGateway;
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

        _logger.LogDebug("Subscribing to events for {Count} listeners", _listeners.Count);
        using var call = await _eventsGateway.SubscribeManyAsync(eventsToWatch, cancellationToken);
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await foreach (var response in call.ReadAllAsync(cancellationToken))
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
        foreach (var intent in _queue.GetConsumingEnumerable(cancellationToken))
            await _dispatcher.DispatchAsync(intent, cancellationToken);
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
