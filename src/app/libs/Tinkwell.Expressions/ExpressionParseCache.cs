using NCalc;
using NCalc.Domain;
using NCalc.Factories;

namespace Tinkwell.Expressions;

/// <summary>
/// Process-global least-recently-used (LRU) cache of parsed NCalc abstract
/// syntax trees, keyed by the raw expression text. Repeated evaluations of
/// the same expression text reuse the cached <see cref="LogicalExpression"/>
/// and skip lex/parse work.
/// </summary>
/// <remarks>
/// <para>
/// The cache is consulted automatically by <see cref="ExpressionEvaluator"/>
/// and <see cref="DependencyWalker{TItem}.ExtractParameters"/>; callers do
/// not need to interact with this type for normal operation. Tune
/// <see cref="Capacity"/> at startup, or set it to <c>0</c> to disable
/// caching entirely.
/// </para>
/// <para>
/// The parsed <see cref="LogicalExpression"/> is treated as immutable; each
/// evaluation builds a fresh <see cref="Expression"/> wrapper so parameters
/// and event handlers are never shared across calls or threads.
/// </para>
/// <para>
/// Parse failures are <strong>not</strong> cached: an invalid expression
/// throws on every call.
/// </para>
/// </remarks>
public static class ExpressionParseCache
{
    private const int DefaultCapacity = 256;

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, LinkedListNode<Entry>> Map =
        new(StringComparer.Ordinal);
    private static readonly LinkedList<Entry> Order = new();

    private static int _capacity = DefaultCapacity;

    /// <summary>
    /// Maximum number of parsed expressions held by the cache. Defaults to
    /// <c>256</c>. Setting a smaller value evicts the oldest entries until
    /// the cache fits; setting <c>0</c> disables caching and clears the
    /// cache. Values below zero are rejected.
    /// </summary>
    public static int Capacity
    {
        get
        {
            lock (SyncRoot)
                return _capacity;
        }
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Capacity must be greater than or equal to zero.");

            lock (SyncRoot)
            {
                _capacity = value;
                TrimToCapacity();
            }
        }
    }

    /// <summary>
    /// Number of expressions currently held by the cache. Useful for
    /// diagnostics and tests.
    /// </summary>
    public static int Count
    {
        get
        {
            lock (SyncRoot)
                return Map.Count;
        }
    }

    /// <summary>
    /// Removes every entry from the cache. Intended for diagnostics and
    /// tests; production code rarely needs this.
    /// </summary>
    public static void Clear()
    {
        lock (SyncRoot)
        {
            Map.Clear();
            Order.Clear();
        }
    }

    /// <summary>
    /// Returns the parsed AST for <paramref name="expression"/>, parsing it
    /// once and reusing the cached value on subsequent calls. Bypasses the
    /// cache when <see cref="Capacity"/> is <c>0</c>.
    /// </summary>
    /// <param name="expression">The raw expression text.</param>
    /// <param name="options">
    /// The <see cref="ExpressionOptions"/> applied during parsing. Internal
    /// callers always pass the library's standard flags.
    /// </param>
    internal static LogicalExpression GetOrParse(string expression, ExpressionOptions options)
    {
        lock (SyncRoot)
        {
            if (_capacity > 0 && Map.TryGetValue(expression, out var node))
            {
                Order.Remove(node);
                Order.AddFirst(node);
                OtMetrics.ParseCacheHits.Add(1);
                return node.Value.Ast;
            }
        }

        var ast = LogicalExpressionFactory.Create(expression, options);

        lock (SyncRoot)
        {
            OtMetrics.ParseCacheMisses.Add(1);

            if (_capacity == 0)
                return ast;

            if (Map.TryGetValue(expression, out var existing))
            {
                Order.Remove(existing);
                Order.AddFirst(existing);
                return existing.Value.Ast;
            }

            var node = new LinkedListNode<Entry>(new Entry(expression, ast));
            Order.AddFirst(node);
            Map[expression] = node;
            TrimToCapacity();
            return ast;
        }
    }

    private static void TrimToCapacity()
    {
        while (Order.Count > _capacity)
        {
            var oldest = Order.Last;
            if (oldest is null)
                break;

            Order.RemoveLast();
            Map.Remove(oldest.Value.Key);
            OtMetrics.ParseCacheEvictions.Add(1);
        }
    }

    private readonly record struct Entry(string Key, LogicalExpression Ast);
}
