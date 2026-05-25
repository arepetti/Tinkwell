using NCalc;

namespace Tinkwell.Expressions;

/// <summary>
/// Analyzes dependencies between items that contain NCalc expressions and
/// produces a topologically sorted calculation order. Generic over
/// <typeparamref name="TItem"/> — the walker does not depend on any
/// domain type.
/// </summary>
/// <remarks>
/// <para>
/// Use this in hosts that have many expressions that may reference one
/// another (e.g. derived fields, dashboard widgets) so you compute
/// or validate an evaluation order. When
/// <see cref="Analyze"/> throws <see cref="CircularDependencyException"/>,
/// <see cref="CircularDependencyException.CycleParticipants"/> is a
/// <em>set of names that could not be placed</em> (see that property’s
/// documentation), not always a single minimal cycle; surface that
/// to users as a group that needs to be unblocked, not a precise loop
/// to render.
/// </para>
/// </remarks>
/// <typeparam name="TItem">
/// The item type. Each item must have a name and an optional expression.
/// </typeparam>
public sealed class DependencyWalker<TItem>
{
    private readonly Func<TItem, string> _nameSelector;
    private readonly Func<TItem, string?> _expressionSelector;

    /// <summary>
    /// Creates a walker that uses the given selectors to obtain each
    /// item's unique name and its optional NCalc expression text.
    /// </summary>
    /// <param name="nameSelector">
    /// Returns the unique name for an item.
    /// </param>
    /// <param name="expressionSelector">
    /// Returns the expression string for an item, or <see langword="null"/>
    /// if the item has no expression (non-derived items).
    /// </param>
    public DependencyWalker(
        Func<TItem, string> nameSelector,
        Func<TItem, string?> expressionSelector)
    {
        _nameSelector = nameSelector ?? throw new ArgumentNullException(nameof(nameSelector));
        _expressionSelector = expressionSelector ?? throw new ArgumentNullException(nameof(expressionSelector));
    }

    /// <summary>
    /// Analyzes the given items and returns the dependency graph with a
    /// topologically sorted calculation order (Kahn's algorithm).
    /// All parameters extracted from expressions appear in the forward
    /// and reverse dependency maps, including references to names that
    /// are not themselves items in the analysis set. The topological
    /// sort only considers items in the input set.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Two or more items resolve to the same name.
    /// </exception>
    /// <exception cref="CircularDependencyException">
    /// A full topological order cannot be produced (a cycle among items, or
    /// items that remain blocked in the Kahn walk). See
    /// <see cref="CircularDependencyException.CycleParticipants"/> for the
    /// reported names and that type's documentation for how to interpret
    /// the set.
    /// </exception>
    public DependencyAnalysis<TItem> Analyze(IEnumerable<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items as IReadOnlyList<TItem> ?? items.ToList();

        var forward = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var reverse = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in itemList)
        {
            var name = _nameSelector(item);
            if (!seenNames.Add(name))
            {
                throw new ArgumentException(
                    $"Duplicate item name '{name}'. Each item in the set must have a unique name.",
                    nameof(items));
            }
            var expression = _expressionSelector(item);

            if (string.IsNullOrWhiteSpace(expression))
            {
                forward[name] = [];
                continue;
            }

            var parameters = ExtractParameters(expression);
            var deps = parameters.Distinct(StringComparer.Ordinal).ToList();
            forward[name] = deps;

            foreach (var dep in deps)
            {
                if (!reverse.TryGetValue(dep, out var dependents))
                {
                    dependents = [];
                    reverse[dep] = dependents;
                }
                dependents.Add(name);
            }
        }

        var calculationOrder = TopologicalSort(itemList, forward, reverse);

        var readonlyReverse = new Dictionary<string, IReadOnlyList<string>>(
            reverse.Count, StringComparer.Ordinal);
        foreach (var (key, value) in reverse)
            readonlyReverse[key] = value;

        return new DependencyAnalysis<TItem>(calculationOrder, forward, readonlyReverse);
    }

    /// <summary>
    /// Extracts parameter names from an NCalc expression string.
    /// </summary>
    public static IReadOnlyList<string> ExtractParameters(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var ast = ExpressionParseCache.GetOrParse(expression, ExpressionEvaluator.DefaultExpressionOptions);
        var expr = new Expression(ast, ExpressionEvaluator.DefaultExpressionOptions);
        return expr.GetParameterNames();
    }

    private IReadOnlyList<TItem> TopologicalSort(
        IReadOnlyList<TItem> items,
        Dictionary<string, IReadOnlyList<string>> forward,
        Dictionary<string, List<string>> reverse)
    {
        var inDegree = new Dictionary<string, int>(items.Count, StringComparer.Ordinal);
        foreach (var item in items)
            inDegree[_nameSelector(item)] = 0;

        foreach (var (name, deps) in forward)
        {
            foreach (var dep in deps)
            {
                if (inDegree.ContainsKey(dep))
                    inDegree[name]++;
            }
        }

        var queue = new Queue<string>();
        foreach (var (name, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(name);
        }

        var sorted = new List<TItem>(items.Count);
        var itemByName = new Dictionary<string, TItem>(items.Count, StringComparer.Ordinal);
        foreach (var item in items)
            itemByName[_nameSelector(item)] = item;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(itemByName[current]);

            if (reverse.TryGetValue(current, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    if (!inDegree.ContainsKey(dependent))
                        continue;

                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }
        }

        if (sorted.Count != items.Count)
        {
            var cycleParticipants = inDegree
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();
            throw new CircularDependencyException(cycleParticipants);
        }

        return sorted;
    }
}

/// <summary>
/// Immutable result of <see cref="DependencyWalker{TItem}.Analyze"/>.
/// </summary>
/// <param name="CalculationOrder">
/// Items in topological order — items with no dependencies first.
/// </param>
/// <param name="ForwardDependencies">
/// Maps each item name to the names it depends on.
/// </param>
/// <param name="ReverseDependencies">
/// Maps each dependency name to the item names that depend on it.
/// </param>
public sealed record DependencyAnalysis<TItem>(
    IReadOnlyList<TItem> CalculationOrder,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ForwardDependencies,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ReverseDependencies);
