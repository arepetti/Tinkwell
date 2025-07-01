using System.Diagnostics;
using NCalc;

namespace Tinkwell.Measures.Configuration.Parser;

public class DependencyWalker<T> where T : IMeasureDependent
{
    // Gets the forward dependency map, where the key is a measure and the value is a list of its direct dependencies.
    public IReadOnlyDictionary<string, IList<string>> ForwardDependencyMap => _forwardDependencyMap;

    // Gets the reverse dependency map, where the key is a dependency and the value is a list of measures that depend on it.
    public IReadOnlyDictionary<string, IList<string>> ReverseDependencyMap => _reverseDependencyMap;

    // Gets the order in which the measures should be calculated, based on their dependencies.
    public IReadOnlyList<string> CalculationOrder => _calculationOrder;

    // Analyzes the dependencies between the derived measures and determines the calculation order.
    // Returns true if the dependencies were successfully resolved and no circular dependencies were found; otherwise, false.
    public bool Analyze(IEnumerable<T> measures)
    {
        _measures = measures;
        _forwardDependencyMap.Clear();
        _reverseDependencyMap.Clear();
        _calculationOrder.Clear();

        ExtractDependencies();
        return ApplyTopologicalSort();
    }

    protected virtual IEnumerable<string> ResolveDependencies(T item)
        => new Expression(item.Expression).GetParameterNames();

    private readonly Dictionary<string, IList<string>> _forwardDependencyMap = new();
    private readonly Dictionary<string, IList<string>> _reverseDependencyMap = new();
    private List<string> _calculationOrder = new();
    private IEnumerable<T> _measures = Enumerable.Empty<T>();

    private void ExtractDependencies()
    {
        foreach (var measure in _measures)
        {
            // NCalc is used to parse the expression and find all external parameters:
            // they're all the (forward) dependencies of this expression!
            var dependencies = ResolveDependencies(measure);
            measure.Dependencies.Clear();
            foreach (var dependency in dependencies)
                measure.Dependencies.Add(dependency);

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
        foreach (var measure in _measures)
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
        foreach (var measure in _measures)
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
        return _calculationOrder.Count == _measures.Count();
    }
}
