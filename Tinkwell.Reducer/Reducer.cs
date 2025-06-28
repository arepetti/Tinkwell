using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using NCalc;
using System.Globalization;
using UnitsNet;

namespace Tinkwell.Reducer;

public class Reducer
{
    private readonly ILogger<Reducer> _logger;
    private readonly Tinkwell.Services.Store.StoreClient _storeClient;
    private List<DerivedMeasure> _derivedMeasures = new();
    private readonly Dictionary<string, List<string>> _forwardDependencyMap = new(); // measure -> its direct dependencies
    private readonly Dictionary<string, List<string>> _reverseDependencyMap = new(); // dependency -> measures that depend on it
    private List<string> _calculationOrder = new();

    public Reducer(ILogger<Reducer> logger)
    {
        _logger = logger;
        // TODO: Discover this from the discovery service
        var channel = GrpcChannel.ForAddress("http://localhost:50051");
        _storeClient = new Tinkwell.Services.Store.StoreClient(channel);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LoadDerivedMeasures();
        ExtractDependencies();
        if (!TopologicalSort())
        {
            _logger.LogCritical("Circular dependency detected in derived measures. Aborting Reducer startup.");
            return;
        }
        await RegisterDerivedMeasuresAsync(cancellationToken);
        await CalculateInitialValuesAsync(cancellationToken);
        await SubscribeToChangesAsync(cancellationToken);
    }

    private void LoadDerivedMeasures()
    {
        _logger.LogInformation("Loading derived measures from derivatives.json");
        var content = File.ReadAllText("measures.twm");
        _derivedMeasures = DerivedMeasureParser.Parse(content).ToList();
    }

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

    private bool TopologicalSort()
    {
        var inDegree = new Dictionary<string, int>();
        foreach (var measure in _derivedMeasures)
        {
            inDegree[measure.Name] = 0;
        }

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
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }
        }

        return _calculationOrder.Count == _derivedMeasures.Count;
    }

    private async Task RegisterDerivedMeasuresAsync(CancellationToken cancellationToken)
    {
        foreach (var measureName in _calculationOrder)
        {
            var measure = _derivedMeasures.First(m => m.Name == measureName);
            _logger.LogDebug("Registering derived measure {Name}", measure.Name);
            await _storeClient.RegisterAsync(new Tinkwell.Services.StoreRegisterRequest
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
        var uniqueDependencies = _forwardDependencyMap.Values.SelectMany(x => x).Distinct().ToList();
        if (uniqueDependencies.Count == 0)
        {
            _logger.LogInformation("No dependencies to subscribe to.");
            return;
        }

        _logger.LogInformation("Subscribing to changes for {Count} dependencies", uniqueDependencies.Count);
        var request = new Tinkwell.Services.SubscribeToSetRequest();
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
        try
        {
            var expression = new NCalc.Expression(measure.Expression);
            var getManyRequest = new Tinkwell.Services.GetManyRequest();
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

            await _storeClient.UpdateAsync(new Tinkwell.Services.StoreUpdateRequest
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

    private static IQuantity FromQuantityProto(Tinkwell.Services.Quantity protoQuantity)
    {
        // UnitsNet.Quantity.From(value, unit) is the most direct way to create an IQuantity from a value and an Enum unit.
        // We need to get the correct Enum value for the unit from the string representation.
        Enum unitEnum = Tinkwell.Reducer.UnitHelpers.ParseUnit(protoQuantity.QuantityType, protoQuantity.Unit);

        // NCalc's Evaluate method returns a double for numeric values, so we use protoQuantity.Number.
        return UnitsNet.Quantity.From(protoQuantity.Number, unitEnum);
    }
}


