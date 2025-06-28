using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NCalc;
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
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        (_storeChannel, _storeClient) = await _discovery.FindServiceAsync(Services.Store.Descriptor.FullName,
            c => new Services.Store.StoreClient(c), cancellationToken);

        _logger.LogInformation("Loading derived measures from measures.twm");
        _derivedMeasures = await _configReader.ReadFromFileAsync("measures.twm", cancellationToken);

        ExtractDependencies();
        if (!ApplyTopologicalSort())
        {
            _logger.LogCritical("Circular dependency detected in derived measures. Aborting Reducer startup.");
            return;
        }

        await RegisterDerivedMeasuresAsync(cancellationToken);
        await CalculateInitialValuesAsync(cancellationToken);
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
    private GrpcChannel? _storeChannel;
    private Services.Store.StoreClient? _storeClient;
    private IEnumerable<DerivedMeasure> _derivedMeasures = [];
    private readonly Dictionary<string, List<string>> _forwardDependencyMap = new(); // measure -> its direct dependencies
    private readonly Dictionary<string, List<string>> _reverseDependencyMap = new(); // dependency -> measures that depend on it
    private List<string> _calculationOrder = new();

    private void ExtractDependencies()
    {
        foreach (var measure in _derivedMeasures)
        {
            var expression = new Expression(measure.Expression);
            var visitor = new DependencyVisitor(new NCalc.ExpressionContext());
            expression.LogicalExpression!.Accept(visitor);
            measure.Dependencies = visitor.Dependencies.ToList();

            _forwardDependencyMap[measure.Name] = measure.Dependencies;

            foreach (var dependency in measure.Dependencies)
            {
                if (!_reverseDependencyMap.ContainsKey(dependency))
                    _reverseDependencyMap[dependency] = new List<string>();

                _reverseDependencyMap[dependency].Add(measure.Name);
            }
        }
    }

    private bool ApplyTopologicalSort()
    {
        // Classic topological sort (https://en.wikipedia.org/wiki/Topological_sorting) using Kahn's algorithm.
        var inDegree = new Dictionary<string, int>();
        foreach (var measure in _derivedMeasures)
            inDegree[measure.Name] = 0;

        foreach (var entry in _forwardDependencyMap)
        {
            foreach (var dependency in entry.Value)
            {
                // Only consider dependencies that are also derived measures
                if (inDegree.ContainsKey(dependency))
                    inDegree[entry.Key]++;
            }
        }

        var queue = new Queue<string>();
        foreach (var measure in _derivedMeasures)
        {
            if (inDegree[measure.Name] == 0)
                queue.Enqueue(measure.Name);
        }

        _calculationOrder.Clear();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            _calculationOrder.Add(current);

            if (_reverseDependencyMap.TryGetValue(current, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }
        }

        return _calculationOrder.Count == _derivedMeasures.Count();
    }

    private async Task RegisterDerivedMeasuresAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        // TODO: add support for Store.RegisterMany() so that we can batch
        // this process instead of calling Store.Register() too many times.
        foreach (var measureName in _calculationOrder)
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
        foreach (var measureName in _calculationOrder)
        {
            var measure = _derivedMeasures.First(m => m.Name == measureName);
            await RecalculateMeasureAsync(measure, cancellationToken);
        }
    }

    private async Task SubscribeToChangesAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        var uniqueDependencies = _forwardDependencyMap.Values.SelectMany(x => x).Distinct().ToList();
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
            _logger.LogWarning("Subscription cancelled.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during subscription: {Message}.", e.Message);
        }
    }

    private async Task HandleChangeAsync(string changedMeasure, CancellationToken cancellationToken)
    {
        if (_reverseDependencyMap.TryGetValue(changedMeasure, out var affectedMeasures))
        {
            // Filter affected measures to only include those that are derived measures
            var derivedAffectedMeasures = affectedMeasures.Where(name => _derivedMeasures.Any(dm => dm.Name == name)).ToList();

            // Recalculate in topological order
            foreach (var measureName in _calculationOrder)
            {
                if (derivedAffectedMeasures.Contains(measureName))
                {
                    var measure = _derivedMeasures.First(m => m.Name == measureName);
                    await RecalculateMeasureAsync(measure, cancellationToken);
                }
            }
        }
    }

    private async Task RecalculateMeasureAsync(DerivedMeasure measure, CancellationToken cancellationToken)
    {
        Debug.Assert(_storeClient is not null);

        try
        {
            var expression = new NCalc.Expression(measure.Expression);
            var getManyRequest = new Services.GetManyRequest();
            getManyRequest.Names.AddRange(measure.Dependencies);

            var response = await _storeClient.GetManyAsync(getManyRequest, cancellationToken: cancellationToken);

            foreach (var dependency in measure.Dependencies)
            {
                if (response.Values.TryGetValue(dependency, out var quantityProto))
                {
                    expression.Parameters[dependency] = FromQuantityProto(quantityProto);
                }
                else
                {
                    _logger.LogWarning("Could not find value for dependency {Dependency} when recalculating {Name}.", dependency, measure.Name);
                    return;
                }
            }

            var result = expression.Evaluate();

            if (result is not IQuantity resultQuantity)
            {
                _logger.LogError("Expression for {Name} did not evaluate to an IQuantity. Result type: {ResultType}", measure.Name, result?.GetType().Name ?? "null");
                return;
            }

            // Apply precision if specified
            if (measure.Precision.HasValue)
            {
                // resultQuantity = resultQuantity.ToUnit(resultQuantity.Unit).Round(measure.Precision.Value); // Commented out for now
            }

            _logger.LogTrace("Recalculated {Name} = {Value}", measure.Name, resultQuantity);

            await _storeClient.UpdateAsync(new Services.StoreUpdateRequest
            {
                Name = measure.Name,
                Value = resultQuantity.ToString("G", CultureInfo.InvariantCulture)
            }, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to recalculate {Name} because: {Message}", measure.Name, e.Message);
        }
    }

    private static IQuantity FromQuantityProto(Services.Quantity protoQuantity)
    {
        // UnitsNet.Quantity.From(value, unit) is the most direct way to create an IQuantity from a value and an Enum unit.
        // We need to get the correct Enum value for the unit from the string representation.
        Enum unitEnum = UnitHelpers.ParseUnit(protoQuantity.QuantityType, protoQuantity.Unit);

        // NCalc's Evaluate method returns a double for numeric values, so we use protoQuantity.Number.
        return UnitsNet.Quantity.From(protoQuantity.Number, unitEnum);
    }
}


