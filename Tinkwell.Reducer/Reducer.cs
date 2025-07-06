using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Reflection;
using Tinkwell.Measures.Configuration.Parser;
using UnitsNet;

namespace Tinkwell.Reducer;

sealed class Reducer : IAsyncDisposable
{
    public Reducer(ILogger<Reducer> logger, ServiceLocator locator, TwmFileReader fileReader, ReducerOptions options)
    {
        _logger = logger;
        _locator = locator;
        _fileReader = fileReader;
        _options = options;
        _dependencyWalker = new();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _store = await _locator.FindStoreAsync(cancellationToken);

        _logger.LogDebug("Loading derived measures from {Path}", _options.Path);
        var file = await _fileReader.ReadFromFileAsync(_options.Path, cancellationToken);
        var measures = file.Measures
            .Where(x => !string.IsNullOrWhiteSpace(x.Expression))
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
            _logger.LogDebug("Reducer started successfully, now watching for changes");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _worker.StopAsync(CancellationToken.None);

            await _locator.DisposeAsync();

        if (_store is not null)
            await _store.DisposeAsync();
    }

    private readonly ILogger<Reducer> _logger;
    private readonly ServiceLocator _locator;
    private readonly TwmFileReader _fileReader;
    private readonly ReducerOptions _options;
    private readonly DependencyWalker<Measure> _dependencyWalker;
    private readonly CancellableLongRunningTask _worker = new();
    private GrpcService<Services.Store.StoreClient>? _store;
    
    private async Task RegisterDerivedMeasuresAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        // TODO: add support for Store.RegisterMany() so that we can batch
        // this process instead of calling Store.Register() too many times.
        foreach (var measureName in _dependencyWalker.CalculationOrder)
        {
            var measure = _dependencyWalker.Items.First(m => m.Name == measureName);
            _logger.LogDebug("Registering derived measure {Name}", measure.Name);
            await _store.Client.RegisterAsync(new Services.StoreRegisterRequest
            {
                Name = measure.Name,
                QuantityType = measure.QuantityType,
                Unit = measure.Unit,
                Minimum = measure.Minimum,
                Maximum = measure.Maximum,
                Category = measure.Category,
                Precision = measure.Precision,
            }, cancellationToken: cancellationToken);
        }
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
        Debug.Assert(_store is not null);

        var uniqueDependencies = _dependencyWalker.ForwardDependencyMap.Values.SelectMany(x => x).Distinct().ToList();
        if (uniqueDependencies.Count == 0)
            return;

        _logger.LogDebug("Subscribing to changes for {Count} dependencies", uniqueDependencies.Count);
        var request = new Services.SubscribeToSetRequest();
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
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // TODO: we should really really REALLY publish these changes in batch: collect all the recalculated
        // measures and then publish them all at once, instead of one by one. We'll need to
        // add a Store.UpdateMany() method. We should also probably have a simpler method where the value
        // is not a string but a number (assumed to be with the correct unit of measure).
        if (_dependencyWalker.ReverseDependencyMap.TryGetValue(changedMeasure, out var affectedMeasures))
        {
            // We only care about the affected measures that are also derived measures.
            var derivedAffectedMeasures = affectedMeasures.Where(name => _dependencyWalker.Items.Any(dm => dm.Name == name)).ToList();

            foreach (var measureName in _dependencyWalker.CalculationOrder)
            {
                if (derivedAffectedMeasures.Contains(measureName))
                {
                    var measure = _dependencyWalker.Items.First(m => m.Name == measureName);
                    await RecalculateMeasureAsync(measure, cancellationToken);
                }
            }
        }

        stopwatch.Stop();
        _logger.LogDebug("Change of '{ChangedMeasureName}' affected {AffectedCount} measure(s) and took {Time} ms to complete",
            changedMeasure, affectedMeasures?.Count ?? 0, stopwatch.ElapsedMilliseconds);
    }

    private async Task RecalculateMeasureAsync(Measure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        if (measure.Disabled)
            return;

        try
        {
            var result = await EvaluateMeasureExpression(measure, cancellationToken);
            if (result is null)
            {
                measure.Disabled = true;
                return;
            }

            if (measure.Precision.HasValue)
                result = UnitHelpers.Round(result, measure.Precision.Value);

            _logger.LogTrace("Recalculated {Name} = {Value}", measure.Name, result);

            await _store.Client.UpdateAsync(new Services.StoreUpdateRequest
            {
                Name = measure.Name,
                Value = result!.ToString("G", CultureInfo.InvariantCulture)
            }, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to recalculate {Name}. Measure disabled. Reason: {Message}", measure.Name, e.Message);
            measure.Disabled = true;
        }
    }

    private async Task<IQuantity?> EvaluateMeasureExpression(Measure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_store is not null);

        var getManyRequest = new Services.GetManyRequest();
        getManyRequest.Names.AddRange(measure.Dependencies);
        var response = await _store.Client.GetManyAsync(getManyRequest, cancellationToken: cancellationToken);

        var expression = new NCalc.Expression(measure.Expression);
        foreach (var dependency in measure.Dependencies)
        {
            if (response.Values.TryGetValue(dependency, out var quantityProto))
            {
                expression.Parameters[dependency] = quantityProto.Number;
            }
            else
            {
                _logger.LogWarning("Could not find value for dependency {Dependency} when recalculating {Name}.", dependency, measure.Name);
                return null;
            }
        }

        var result = ConvertResultToQuantity(measure, expression.Evaluate());
        if (result is null)
        {
            _logger.LogError("Expression for {Name} did not evaluate to manageable quantity. Measure disabled. Result type: {ResultType}", measure.Name, result?.GetType().Name ?? "null");
            return null;
        }

        return result;
    }

    private IQuantity? ConvertResultToQuantity(Measure measure, object? result)
    {
        if (result is null)
            return null;

        if (result is IQuantity)
            return (IQuantity)result;

        if (UnitHelpers.TryGetQuantityValue(result, out var value))
            return Quantity.From(value, measure.QuantityType, measure.Unit);

        return null;
    }
}


