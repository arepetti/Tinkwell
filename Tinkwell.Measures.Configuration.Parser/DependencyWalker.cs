using System.Diagnostics;
using NCalc;

namespace Tinkwell.Measures.Configuration.Parser;

public class DependencyWalker<T> where T : IMeasureDependent
{
    public IEnumerable<T> Items => _items;

    // Gets the forward dependency map, where the key is a item and the value is a list of its direct dependencies.
    public IReadOnlyDictionary<string, IList<string>> ForwardDependencyMap => _forwardDependencyMap;

    // Gets the reverse dependency map, where the key is a dependency and the value is a list of measures that depend on it.
    public IReadOnlyDictionary<string, IList<string>> ReverseDependencyMap => _reverseDependencyMap;

    // Gets the order in which the measures should be calculated, based on their dependencies.
    public IReadOnlyList<string> CalculationOrder => _calculationOrder;

    // Analyzes the dependencies between the derived measures and determines the calculation order.
    // Returns true if the dependencies were successfully resolved and no circular dependencies were found; otherwise, false.
    public bool Analyze(IEnumerable<T> measures)
    {
        _items = measures;

        foreach (var item in _items)
            item.Dependencies.Clear();

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
    private IEnumerable<T> _items = Enumerable.Empty<T>();

    private void ExtractDependencies()
    {
        foreach (var item in _items)
        {
            // NCalc is used to parse the expression and find all external parameters:
            // they're all the (forward) dependencies of this expression!
            foreach (var dependency in ResolveDependencies(item))
            {
                item.Dependencies.Add(dependency);

                // The reverse map is for efficient lookup of dependents
                if (!_reverseDependencyMap.ContainsKey(dependency))
                    _reverseDependencyMap[dependency] = [];

                _reverseDependencyMap[dependency].Add(item.Name);
            }

            _forwardDependencyMap[item.Name] = item.Dependencies;
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
        foreach (var item in _items)
            inDegree[item.Name] = 0;

        // Calculate the in-degree for each item (number of dependencies it has).
        foreach (var entry in _forwardDependencyMap)
        {
            foreach (var dependency in entry.Value)
            {
                // We only care about dependencies that are also items themselves
                if (inDegree.ContainsKey(dependency))
                    inDegree[entry.Key]++;
            }
        }

        // Initialize the queue with all items that have an in-degree of 0 (no dependencies)
        var queue = new Queue<string>();
        foreach (var measure in _items)
        {
            if (inDegree[measure.Name] == 0)
                queue.Enqueue(measure.Name);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            _calculationOrder.Add(current);

            // For the current item, find all items that depend on it (dependents)
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

        // If the number of items in the sorted list is equal to the total number of items,
        // the sort was successful and there are no circular dependencies.
        return _calculationOrder.Count == _items.Count();
    }
}
