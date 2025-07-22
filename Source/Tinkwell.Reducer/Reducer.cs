using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Reflection;
using Tinkwell.Measures.Configuration.Parser;
using Tinkwell.Services;
using Tinkwell.Services.Proto.Proxies;

namespace Tinkwell.Reducer;

sealed class Reducer : IAsyncDisposable
{
    public Reducer(ILogger<Reducer> logger, IStore store, IConfigFileReader<ITwmFile> fileReader, ReducerOptions options)
    {
        _logger = logger;
        _store = store;
        _fileReader = fileReader;
        _options = options;
        _dependencyWalker = new();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = HostingInformation.GetFullPath(_options.Path);
        _logger.LogDebug("Loading derived measures from {Path}", path);

        var file = await _fileReader.ReadAsync(path, cancellationToken);
        var measures = file.Measures
            .Select(x => ShallowCloner.CopyAllPublicProperties(x, new Measure()))
            .ToArray();

        // We need to analyze the dependencies between the derived measures to determine the correct calculation order.
        // This is crucial to ensure that we don't try to calculate a measure before its dependencies are calculated.
        if (!_dependencyWalker.Analyze(measures))
        {
            _logger.LogCritical("Circular dependency detected in derived measures. Aborting Reducer startup.");
            return;
        }

        // If there are no measures to calculate we do not subscribe to anything and just terminate here
        if (!_dependencyWalker.Items.Any())
        {
            _logger.LogWarning("No derived measures to calculate, Reducer is going to sit idle.");
            return;
        }

        _logger.LogDebug("Calculating and registering the new measures");
        await RegisterDerivedMeasuresAsync(cancellationToken);
        await CalculateInitialValuesAsync(cancellationToken);

        // If there are no measures with dependencies then we do not subscribe to anything
        if (_dependencyWalker.ForwardDependencyMap.Count == 0)
        {
            _logger.LogWarning("No measures with dependencies to watch, Reducer is going to sit idle.");
            return;
        }
        else
        {
            await _worker.StartAsync(SubscribeToChangesAsync, cancellationToken);
            _logger.LogInformation("Reducer started successfully");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _worker.StopAsync(CancellationToken.None);

        if (_store is not null)
            await _store.DisposeAsync();
    }

    private readonly ILogger<Reducer> _logger;
    private readonly IStore _store;
    private readonly IConfigFileReader<ITwmFile> _fileReader;
    private readonly ReducerOptions _options;
    private readonly DependencyWalker<Measure> _dependencyWalker;
    private readonly CancellableLongRunningTask _worker = new();

    private async Task RegisterDerivedMeasuresAsync(CancellationToken cancellationToken)
    {
        var request = new StoreRegisterManyRequest();
        foreach (var measureName in _dependencyWalker.CalculationOrder)
        {
            _logger.LogDebug("Registering derived measure {Name}", measureName);
            var measure = _dependencyWalker.Items.First(m => m.Name == measureName);
            request.Items.Add(measure.ToStoreRegisterRequest(_options.UseConstants));
        }
        await _store.RegisterManyAsync(request, cancellationToken);
    }

    private async Task CalculateInitialValuesAsync(CancellationToken cancellationToken)
    {
        foreach (var measureName in _dependencyWalker.CalculationOrder)
        {
            var measure = _dependencyWalker.Items.First(m => m.Name == measureName);
            await RecalculateMeasureAsync(measure, cancellationToken);
        }
    }

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
        // TODO: we should really really REALLY publish these changes in batch: collect all the recalculated
        // measures and then publish them all at once, instead of one by one.
        if (_dependencyWalker.ReverseDependencyMap.TryGetValue(changedMeasure, out var affectedMeasures))
        {
            // We only care about the affected measures that are also derived measures.
            var derivedAffectedMeasures = affectedMeasures
                .Where(name => _dependencyWalker.Items.Any(dm => dm.Name == name))
                .ToList();

            // We walk through the measures in the order we have to calculate them
            // (it's like sorting derivedAffectedMeasures by the index in CalculationOrder
            // but slightly faster), when a measure has been affected by this change then
            // we need to recalculate it.
            foreach (var measureName in _dependencyWalker.CalculationOrder)
            {
                if (derivedAffectedMeasures.Contains(measureName))
                {
                    var measure = _dependencyWalker.Items.First(m => m.Name == measureName);
                    await RecalculateMeasureAsync(measure, cancellationToken);
                }
            }
        }
    }

    private async Task RecalculateMeasureAsync(Measure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        // No op if this measure has been disabled before because an update failed or if
        // it's just a "declaration" we do not update directly.
        if (measure.Disabled || string.IsNullOrWhiteSpace(measure.Expression))
            return;

        try
        {
            // First calculate the new value
            var result = await EvaluateMeasureExpression(measure, cancellationToken);
            if (result is null)
            {
                measure.Disabled = true;
                return;
            }

            // Then write it to the store
            _logger.LogTrace("Recalculated {Name} = {Value}", measure.Name, result);
            await _store.WriteQuantityAsync(measure.Name, result.Value, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to recalculate {Name}. Measure disabled. Reason: {Message}", measure.Name, e.Message);
            measure.Disabled = true;
        }
    }

    private async Task<double?> EvaluateMeasureExpression(Measure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        // First we need the current value for all the dependencies, they'll become
        // parameters for the NCalc expression.
        var values = await _store.ReadManyAsync(measure.Dependencies, cancellationToken);

        // The we can calculate the result, it's always a double: derived measures do not
        // support a string output (but they accept strings as inputs!)
        var expression = new ExpressionEvaluator();
        if (expression.TryEvaluateDouble(measure.Expression, values.ToDictionary(), out var result))
            return result;

        _logger.LogError("Expression for {Name} did not evaluate to manageable quantity. Measure disabled.", measure.Name);
        return null;
    }
}
