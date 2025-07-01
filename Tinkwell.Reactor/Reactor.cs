using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Services;

namespace Tinkwell.Reactor;

sealed class Reactor : IAsyncDisposable
{
    public Reactor(ILogger<Reactor> logger, ServiceLocator locator, SignalListConfigReader configReader, ReactorOptions options)
    {
        _logger = logger;
        _locator = locator;
        _configReader = configReader;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _store = await _locator.FindStoreAsync(cancellationToken);
        _eventsGateway = await _locator.FindEventsGatewayAsync(cancellationToken);

        _logger.LogDebug("Loading signals from {Path}", _options.Path);
        _signals = await _configReader.ReadFromFileAsync(_options.Path, cancellationToken);

        if (!_signals.Any())
        {
            _logger.LogWarning("No signals to monitor, Reactor is going to sit idle.");
            return;
        }

        // We can safely ignore the return value, signals cannot have circular dependencies
        Trace.Assert(_dependencyWalker.Analyze(_signals));

        if (_options.CheckOnStartup)
            await CheckAllConditionsAsync(cancellationToken);

        await _worker.StartAsync(SubscribeToChangesAsync);
        _logger.LogInformation("Reactor started successfully, now watching for changes");
    }

    public async ValueTask DisposeAsync()
    {
        await _worker.StopAsync(CancellationToken.None);

        if (_locator is not null)
            await _locator.DisposeAsync();

        if (_store is not null)
            await _store.DisposeAsync();

        if (_eventsGateway is not null)
            await _eventsGateway.DisposeAsync();
    }

    private readonly ILogger<Reactor> _logger;
    private readonly ServiceLocator _locator;
    private readonly SignalListConfigReader _configReader;
    private readonly ReactorOptions _options;
    private readonly CancellableLongRunningTask _worker = new();
    private readonly SignalDependencyWalker _dependencyWalker = new();
    private IEnumerable<Signal> _signals = [];
    private GrpcService<Store.StoreClient>? _store;
    private GrpcService<EventsGateway.EventsGatewayClient>? _eventsGateway;

    private async Task SubscribeToChangesAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        var uniqueDependencies = _dependencyWalker.ForwardDependencyMap.Values.SelectMany(x => x).Distinct().ToList();
        if (uniqueDependencies.Count == 0)
            return;

        _logger.LogDebug("Subscribing to changes for {Count} dependencies", uniqueDependencies.Count);
        var request = new SubscribeToSetRequest();
        request.Names.AddRange(uniqueDependencies);

        using var call = _store.Client.SubscribeToSet(request, cancellationToken: cancellationToken);
        try
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                foreach (var change in response.Changes)
                    await HandleChangeAsync(change.Name, cancellationToken);
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug("Subscription cancelled by the host.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during subscription: {Message}.", e.Message);
        }
    }

    private async Task HandleChangeAsync(string changedMeasure, CancellationToken cancellationToken)
    {
        if (!_dependencyWalker.ReverseDependencyMap.TryGetValue(changedMeasure, out var affectedSignals))
            return;

        var signalsToCalculate = _dependencyWalker.CalculationOrder.Where(x => affectedSignals.Contains(x));
        foreach (var signalName in signalsToCalculate)
        {
            var measure = _signals.First(m => m.Name == signalName);
            await CheckConditionAsync(measure, tryWait: false, cancellationToken);
        }
    }

    private async Task CheckAllConditionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking for all conditions already met");
        foreach (var signal in _signals)
            await CheckConditionAsync(signal, tryWait: true, cancellationToken);
    }

    private async Task CheckConditionAsync(Signal signal, bool tryWait, CancellationToken cancellationToken)
    {
        if (signal.Disabled || cancellationToken.IsCancellationRequested)
            return;

        for (int i = 0; i < 3; ++i)
        {
            try
            {
                var active = await EvaluateConditionAsync(signal, cancellationToken);
                _logger.LogTrace("Recalculated {Name} = {Value}", signal.Name, active);

                if (active.HasValue && active.Value)
                    await PublishEventAsync(signal, cancellationToken);

                return;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                // When bootstrapping another runner may misbehave and claim it's ready before it finished
                // to register all its measures. Not a big deal, we just wait.
                if (tryWait)
                {
                    _logger.LogWarning("Waiting for the sytem to complete the initialization process.");
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                _logger.LogError(e, "Dependencies of {Name} not available. Measure disabled. Reason: {Message}", signal.Name, e.Message);
                signal.Disabled = true;
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to recalculate {Name}. Measure disabled. Reason: {Message}", signal.Name, e.Message);
                signal.Disabled = true;
                return;
            }
        }
    }

    private async Task<bool?> EvaluateConditionAsync(Signal signal, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        // Note that signals can use a special variable named "value" to reference the owning measure.
        // If there is a measure in the system with that name then it has the precedence otherwise
        // we set the parameter value to whatever value the owning measure has. The implicit dependency
        // is added automatically by SignalDependencyWalker, so we don't need to worry about it.
        string? owningMeasure = signal.Owner;
        bool hasOwningMeasure = !string.IsNullOrWhiteSpace(owningMeasure);

        var getManyRequest = new Services.GetManyRequest();
        getManyRequest.Names.AddRange(signal.Dependencies);

        var response = await _store.Client.GetManyAsync(getManyRequest, cancellationToken: cancellationToken);

        var expression = new NCalc.Expression(signal.When);
        foreach (var dependency in signal.Dependencies)
        {
            if (response.Values.TryGetValue(dependency, out var quantityProto))
            {
                expression.Parameters[dependency] = quantityProto.Number;
            }
            else if (string.Equals(dependency, "value", StringComparison.Ordinal) && hasOwningMeasure)
            {
                if (response.Values.TryGetValue(owningMeasure, out var owningQuantityProto))
                    expression.Parameters["value"] = owningQuantityProto.Number;
            }

            return null;
        }

        return Convert.ToBoolean(expression.Evaluate(), CultureInfo.InvariantCulture);
    }

    private async Task PublishEventAsync(Signal signal, CancellationToken cancellationToken)
    {
        Debug.Assert(_eventsGateway is not null);

        await _eventsGateway.Client.PublishAsync(new()
        {
            Topic = signal.Topic ?? WellKnownNames.EventTopicSignal,
            Subject = FromPayload(nameof(PublishEventsRequest.Subject), signal.Owner ?? "?"),
            Verb = FromPayloadEnum(nameof(PublishEventsRequest.Verb), Verb.Triggered),
            Object = FromPayload(nameof(PublishEventsRequest.Object), signal.Name),
        });

        string FromPayload(string propertyName, string defaultValue)
        {
            if (!signal.Payload.TryGetValue(propertyName.ToLower(), out var value))
                return defaultValue;

            string? text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? defaultValue : text.Trim();
        }

        T FromPayloadEnum<T>(string propertyName, T defaultValue)
            where T : struct
        {
            string value = FromPayload(propertyName, defaultValue.ToString()!);
            if (Enum.TryParse<T>(value, true, out var result))
                return result;

            return defaultValue;
        }
    }
}
