using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Tinkwell.Reducer.Parser;
using UnitsNet;

namespace Tinkwell.Reducer;

sealed class Reducer : IAsyncDisposable
{
    public Reducer(ILogger<Reducer> logger, DiscoveryHelper discovery, MeasureListConfigReader configReader, ReducerOptions options)
    {
        _logger = logger;
        _discovery = discovery;
        _configReader = configReader;
        _options = options;
        _dependencyWalker = new();
    }

    // This method ends when the subscription is interrupted!
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        (_storeChannel, _storeClient) = await _discovery.FindServiceAsync(Services.Store.Descriptor.FullName,
            c => new Services.Store.StoreClient(c), cancellationToken);

        _logger.LogDebug("Loading derived measures from {Path}", _options.Path);
        _derivedMeasures = await _configReader.ReadFromFileAsync(_options.Path, cancellationToken);

        if (!_derivedMeasures.Any())
        {
            _logger.LogWarning("No derived measures to calculate, Reducer is going to sit idle.");
            return;
        }

        if (!_dependencyWalker.Analyze(_derivedMeasures))
        {
            _logger.LogCritical("Circular dependency detected in derived measures. Aborting Reducer startup.");
            return;
        }

        _logger.LogDebug("Calculating and registering the new measures");
        await RegisterDerivedMeasuresAsync(cancellationToken);
        await CalculateInitialValuesAsync(cancellationToken);

        _logger.LogInformation("Reducer started successfully, now subscribing for changes");
        await SubscribeToChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_discovery is not null)
            await _discovery.DisposeAsync();

        if (_storeChannel is not null)
        {
            await _storeChannel.ShutdownAsync();
            _storeChannel.Dispose();
        }
    }

    private readonly ILogger<Reducer> _logger;
    private readonly DiscoveryHelper _discovery;
    private readonly MeasureListConfigReader _configReader;
    private readonly ReducerOptions _options;
    private readonly DependencyWalker _dependencyWalker;
    private GrpcChannel? _storeChannel;
    private Services.Store.StoreClient? _storeClient;
    private IEnumerable<DerivedMeasure> _derivedMeasures = [];

    private async Task RegisterDerivedMeasuresAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        // TODO: add support for Store.RegisterMany() so that we can batch
        // this process instead of calling Store.Register() too many times.
        foreach (var measureName in _dependencyWalker.CalculationOrder)
        {
            var measure = _derivedMeasures.First(m => m.Name == measureName);
            _logger.LogDebug("Registering derived measure {Name}", measure.Name);
            await _storeClient.RegisterAsync(new Services.StoreRegisterRequest
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
            var measure = _derivedMeasures.First(m => m.Name == measureName);
            await RecalculateMeasureAsync(measure, cancellationToken);
        }
    }

    private async Task SubscribeToChangesAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        var uniqueDependencies = _dependencyWalker.ForwardDependencyMap.Values.SelectMany(x => x).Distinct().ToList();
        if (uniqueDependencies.Count == 0)
            return;

        _logger.LogDebug("Subscribing to changes for {Count} dependencies", uniqueDependencies.Count);
        var request = new Services.SubscribeToSetRequest();
        request.Names.AddRange(uniqueDependencies);

        using var call = _storeClient.SubscribeToSet(request, cancellationToken: cancellationToken);
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

        if (_dependencyWalker.ReverseDependencyMap.TryGetValue(changedMeasure, out var affectedMeasures))
        {
            // Filter affected measures to only include those that are derived measures
            var derivedAffectedMeasures = affectedMeasures.Where(name => _derivedMeasures.Any(dm => dm.Name == name)).ToList();

            // Recalculate in topological order
            foreach (var measureName in _dependencyWalker.CalculationOrder)
            {
                if (derivedAffectedMeasures.Contains(measureName))
                {
                    var measure = _derivedMeasures.First(m => m.Name == measureName);
                    await RecalculateMeasureAsync(measure, cancellationToken);
                }
            }
        }

        stopwatch.Stop();
        _logger.LogDebug("Change of '{ChangedMeasureName}' affected {AffectedCount} measure(s) and took {Time} ms to complete",
            changedMeasure, affectedMeasures?.Count ?? 0, stopwatch.ElapsedMilliseconds);
    }

    private async Task RecalculateMeasureAsync(DerivedMeasure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        if (measure.Disabled)
            return;

        try
        {
            var getManyRequest = new Services.GetManyRequest();
            getManyRequest.Names.AddRange(measure.Dependencies);
            var response = await _storeClient.GetManyAsync(getManyRequest, cancellationToken: cancellationToken);

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
                    return;
                }
            }

            var result = ConvertResultToQuantity(measure, expression.Evaluate());
            if (result is null)
            {
                _logger.LogError("Expression for {Name} did not evaluate to manageable quantity. Measure disabled. Result type: {ResultType}", measure.Name, result?.GetType().Name ?? "null");
                measure.Disabled = true;
                return;
            }

            if (measure.Precision.HasValue)
                result = UnitHelpers.Round(result, measure.Precision.Value);

            _logger.LogTrace("Recalculated {Name} = {Value}", measure.Name, result);

            await _storeClient.UpdateAsync(new Services.StoreUpdateRequest
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

    private IQuantity? ConvertResultToQuantity(DerivedMeasure measure, object? result)
    {
        if (result is null)
            return null;

        if (result is IQuantity)
            return (IQuantity)result;

        if (UnitHelpers.TryGetQuantityValue(result, out var value))
            return Quantity.From(value, measure.QuantityType, measure.Unit);

        return null;
    }

    //private static IQuantity FromQuantityProto(Services.Quantity protoQuantity)
    //{
    //    // UnitsNet.Quantity.From(value, unit) is the most direct way to create an IQuantity from a value and an Enum unit.
    //    // We need to get the correct Enum value for the unit from the string representation.
    //    Enum unitEnum = UnitHelpers.ParseUnit(protoQuantity.QuantityType, protoQuantity.Unit);

    //    // NCalc's Evaluate method returns a double for numeric values, so we use protoQuantity.Number.
    //    return Quantity.From(protoQuantity.Number, unitEnum);
    //}
}


