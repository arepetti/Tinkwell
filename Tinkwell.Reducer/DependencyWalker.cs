using System.Diagnostics;
using NCalc;

using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Reducer;

sealed class DependencyWalker
{
    // Gets the forward dependency map, where the key is a measure and the value is a list of its direct dependencies.
    public IReadOnlyDictionary<string, List<string>> ForwardDependencyMap => _forwardDependencyMap;

    // Gets the reverse dependency map, where the key is a dependency and the value is a list of measures that depend on it.
    public IReadOnlyDictionary<string, List<string>> ReverseDependencyMap => _reverseDependencyMap;

    // Gets the order in which the measures should be calculated, based on their dependencies.
    public IReadOnlyList<string> CalculationOrder => _calculationOrder;

    // Analyzes the dependencies between the derived measures and determines the calculation order.
    // Returns true if the dependencies were successfully resolved and no circular dependencies were found; otherwise, false.
    public bool Analyze(IEnumerable<MeasureDefinition> derivedMeasures)
    {
        _derivedMeasures = derivedMeasures;
        _forwardDependencyMap.Clear();
        _reverseDependencyMap.Clear();
        _calculationOrder.Clear();

        ExtractDependencies();
        return ApplyTopologicalSort();
    }

    private readonly Dictionary<string, List<string>> _forwardDependencyMap = new();
    private readonly Dictionary<string, List<string>> _reverseDependencyMap = new();
    private List<string> _calculationOrder = new();
    private IEnumerable<MeasureDefinition> _derivedMeasures = Enumerable.Empty<MeasureDefinition>();

    private void ExtractDependencies()
    {
        foreach (var measure in _derivedMeasures)
        {
            // NCalc is used to parse the expression and find all external parameters:
            // they're all the (forward) dependencies of this expression!
            var expression = new Expression(measure.Expression);
            measure.Dependencies = expression.GetParameterNames().ToList();

            _forwardDependencyMap[measure.Name] = measure.Dependencies;

            // Build the reverse map for efficient lookup of dependents
            foreach (var dependency in measure.Dependencies)
            {
                if (!_reverseDependencyMap.ContainsKey(dependency))
                    _reverseDependencyMap[dependency] = new List<string>();
                _reverseDependencyMap[dependency].Add(measure.Name);
            }
        }
    }

    // Applies a topological sort to the dependency graph to determine the calculation order.
    // This method uses Kahn's algorithm.
    // For details on the algorithm, see: https://en.wikipedia.org/wiki/Topological_sorting
    // Returns true if the dependencies were successfully resolved and no circular dependencies were found; otherwise, false.
    private bool ApplyTopologicalSort()
    {
        Debug.Assert(_calculationOrder.Count == 0);

        var inDegree = new Dictionary<string, int>();
        foreach (var measure in _derivedMeasures)
            inDegree[measure.Name] = 0;

        // Calculate the in-degree for each measure (number of dependencies it has).
        foreach (var entry in _forwardDependencyMap)
        {
            foreach (var dependency in entry.Value)
            {
                // We only care about dependencies that are also derived measures themselves
                if (inDegree.ContainsKey(dependency))
                    inDegree[entry.Key]++;
            }
        }

        // Initialize the queue with all measures that have an in-degree of 0 (no dependencies)
        var queue = new Queue<string>();
        foreach (var measure in _derivedMeasures)
        {
            if (inDegree[measure.Name] == 0)
                queue.Enqueue(measure.Name);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            _calculationOrder.Add(current);

            // For the current measure, find all measures that depend on it (dependents)
            if (_reverseDependencyMap.TryGetValue(current, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    // Decrement the in-degree of each dependent. If it becomes 0, add it to the queue
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }
        }

        // If the number of measures in the sorted list is equal to the total number of measures,
        // the sort was successful and there are no circular dependencies.
        return _calculationOrder.Count == _derivedMeasures.Count();
    }
}