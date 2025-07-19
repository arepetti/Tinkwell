using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Reflection;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Services;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Reactor;

sealed class Reactor : IAsyncDisposable
{
    public Reactor(ILogger<Reactor> logger, IConfigFileReader<ITwmFile> fileReader, IStore store, IEventsGateway eventsGateway, ReactorOptions options)
    {
        _logger = logger;
        _store = store;
        _eventsGateway = eventsGateway;
        _fileReader = fileReader;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = HostingInformation.GetFullPath(_options.Path);
        _logger.LogDebug("Loading signals from {Path}", path);
        var file = await _fileReader.ReadAsync(path, FileReaderOptions.Default, cancellationToken);

        var rootSignals = file.Signals.Select(signal => ShallowCloner.CopyAllPublicProperties(signal, new Signal()));
        var dependentSignals = file.Measures.SelectMany(measure =>
            measure.Signals.Select(signal => ShallowCloner.CopyAllPublicProperties(signal, new Signal(measure.Name))));
        var allSignals = Enumerable.Concat(dependentSignals, rootSignals).ToArray();

        // We can safely ignore the return value, signals cannot have circular dependencies
        Trace.Assert(_dependencyWalker.Analyze(allSignals));

        if (!_dependencyWalker.Items.Any())
        {
            _logger.LogWarning("No signals to monitor, Reactor is going to sit idle.");
            return;
        }

        if (_options.CheckOnStartup)
            await CheckAllConditionsAsync(cancellationToken);

        await _worker.StartAsync(SubscribeToChangesAsync);
        _logger.LogDebug("Reactor started successfully, now watching for changes");
        _subscriptionReadyTcs.SetResult(true);
    }

    public async ValueTask DisposeAsync()
        => await _worker.StopAsync(CancellationToken.None);

    private const int NumberOfRetriesOnError = 3;
    private const int DelayBeforeRetryingOnError = 1000;

    private readonly ILogger<Reactor> _logger;
    private readonly IConfigFileReader<ITwmFile> _fileReader;
    private readonly IStore _store;
    private readonly IEventsGateway _eventsGateway;
    private readonly ReactorOptions _options;
    private readonly CancellableLongRunningTask _worker = new();
    private readonly SignalDependencyWalker _dependencyWalker = new();
    private readonly TaskCompletionSource<bool> _subscriptionReadyTcs = new();

    public Task WaitForSubscriptionReadyAsync() => _subscriptionReadyTcs.Task;

    private async Task SubscribeToChangesAsync(CancellationToken cancellationToken)
    {
        var uniqueDependencies = _dependencyWalker.ForwardDependencyMap.Values
            .SelectMany(x => x)
            .Distinct()
            .ToList();

        if (uniqueDependencies.Count == 0)
            return;

        _logger.LogDebug("Subscribing to changes for {Count} dependencies", uniqueDependencies.Count);
        using var call = await _store.SubscribeManyAsync(uniqueDependencies, cancellationToken);
        try
        {
            await foreach (var response in call.ReadAllAsync(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break; // Explicitly break if cancellation is requested
                await HandleChangeAsync(response.Name, cancellationToken);
            }
        }
        catch (RpcException e)
        {
            if (e.StatusCode == StatusCode.Cancelled)
                _logger.LogDebug("Subscription cancelled by the host.");
            else if (e.StatusCode != StatusCode.Unavailable) // Unavailable might happen when closing
                throw;
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

        var signalsToCalculate = _dependencyWalker.CalculationOrder.Where(affectedSignals.Contains);
        foreach (var signalName in signalsToCalculate)
        {
            var signal = _dependencyWalker.Items.First(m => m.Name == signalName);
            await CheckConditionAsync(signal, tryWait: false, cancellationToken);
        }
    }

    private async Task CheckAllConditionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking for all conditions already met");
        foreach (var signal in _dependencyWalker.Items)
            await CheckConditionAsync(signal, tryWait: true, cancellationToken);
    }

    private async Task CheckConditionAsync(Signal signal, bool tryWait, CancellationToken cancellationToken)
    {
        if (signal.Disabled || cancellationToken.IsCancellationRequested)
            return;

        // When bootstrapping another runner may misbehave and claim it's ready before it finished
        // to register all its measures. Not a big deal, we just wait and retry a few times (but only
        // when bootstrapping!).
        for (int i = 0; i < NumberOfRetriesOnError; ++i)
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
                if (tryWait)
                {
                    _logger.LogWarning("Waiting for the sytem to complete the initialization process.");
                    await Task.Delay(DelayBeforeRetryingOnError, cancellationToken);
                    continue;
                }

                _logger.LogError(e, "Dependencies of {Name} not available. Signal disabled. Reason: {Message}", signal.Name, e.Message);
                signal.Disabled = true;
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to recalculate {Name}. Signal disabled. Reason: {Message}", signal.Name, e.Message);
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

        var response = await _store.ReadManyAsync(signal.Dependencies, cancellationToken);
        if (response.Items.Any(x => x.Value.PayloadCase == StoreValue.PayloadOneofCase.None))
            return null; // No value? The measure is still undefined

        var parameters = response.ToDictionary();

        if (hasOwningMeasure)
        {
            var owningMeasureValue = parameters[owningMeasure!];
            if (owningMeasureValue is not null && !parameters.ContainsKey("value"))
                parameters["value"] = owningMeasureValue;
        }

        return new ExpressionEvaluator().EvaluateBool(signal.When, parameters);
    }

    private async Task PublishEventAsync(Signal signal, CancellationToken cancellationToken)
    {
        Debug.Assert(_eventsGateway is not null);

        await _eventsGateway.PublishAsync(new()
        {
            Topic = signal.Topic ?? WellKnownNames.EventTopicSignal,
            Subject = FromPayload(nameof(PublishEventsRequest.Subject), signal.Owner ?? "?"),
            Verb = FromPayloadEnum(nameof(PublishEventsRequest.Verb), Verb.Triggered),
            Object = FromPayload(nameof(PublishEventsRequest.Object), signal.Name),
            Payload = System.Text.Json.JsonSerializer.Serialize(signal.Payload),
        }, cancellationToken);

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
