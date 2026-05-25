using System.Collections;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>count(coll)</c> — Returns the number of elements in the collection.
/// Returns <c>0</c> when the collection is <see langword="null"/>.
/// </summary>
sealed class Count : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
        => arg?.Cast<object>().Count() ?? 0;
}

/// <summary>
/// <c>at(coll, index)</c> — Returns the element at the zero-based
/// <c>index</c>. Returns <see langword="null"/> when the collection is
/// <see langword="null"/> or the index is out of range.
/// </summary>
sealed class At : BinaryFunction<IEnumerable?, int>
{
    protected override object? Call(IEnumerable? arg, int index)
        => arg?.Cast<object>().Skip(index).FirstOrDefault();
}

/// <summary>
/// <c>skip(coll, count)</c> — Skips the first <c>count</c> elements of the
/// collection. Returns <see langword="null"/> when the collection is
/// <see langword="null"/>.
/// </summary>
sealed class SkipItems : BinaryFunction<IEnumerable?, int>
{
    public override string Name => "skip";

    protected override object? Call(IEnumerable? arg, int count)
        => arg?.Cast<object>().Skip(count);
}

/// <summary>
/// <c>take(coll, count)</c> — Returns the first <c>count</c> elements of the
/// collection. Returns <see langword="null"/> when the collection is
/// <see langword="null"/>.
/// </summary>
sealed class TakeItems : BinaryFunction<IEnumerable?, int>
{
    public override string Name => "take";

    protected override object? Call(IEnumerable? arg, int count)
        => arg?.Cast<object>().Take(count);
}

/// <summary>
/// <c>first(coll)</c> — Returns the first element of the collection, or
/// <see langword="null"/> when the collection is <see langword="null"/> or
/// empty.
/// </summary>
sealed class First : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
        => arg?.Cast<object>().FirstOrDefault();
}

/// <summary>
/// <c>last(coll)</c> — Returns the last element of the collection, or
/// <see langword="null"/> when the collection is <see langword="null"/> or
/// empty.
/// </summary>
sealed class Last : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
        => arg?.Cast<object>().LastOrDefault();
}

/// <summary>
/// <c>sum(coll)</c> — Sums the elements of the collection using
/// <see cref="Convert.ToDouble(object?)"/>. Returns <see langword="null"/>
/// when the collection is <see langword="null"/>; returns <c>0</c> for an
/// empty collection. Throws when an element cannot be converted to a number.
/// </summary>
sealed class Sum : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
    {
        if (arg is null)
            return null;
        return arg.Cast<object>().Sum(Convert.ToDouble);
    }
}

/// <summary>
/// <c>avg(coll)</c> — Returns the arithmetic mean of the elements of the
/// collection. Returns <see langword="null"/> when the collection is
/// <see langword="null"/>; throws when the collection is empty or contains
/// non-numeric elements.
/// </summary>
sealed class Avg : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
    {
        if (arg is null)
            return null;
        return arg.Cast<object>().Average(Convert.ToDouble);
    }
}

/// <summary>
/// <c>min(coll)</c> — Returns the minimum element of the collection after
/// numeric conversion. Returns <see langword="null"/> when the collection
/// is <see langword="null"/>; throws when the collection is empty or
/// contains non-numeric elements.
/// </summary>
sealed class Min : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
    {
        if (arg is null)
            return null;
        return arg.Cast<object>().Min(Convert.ToDouble);
    }
}

/// <summary>
/// <c>max(coll)</c> — Returns the maximum element of the collection after
/// numeric conversion. Returns <see langword="null"/> when the collection
/// is <see langword="null"/>; throws when the collection is empty or
/// contains non-numeric elements.
/// </summary>
sealed class Max : UnaryFunction<IEnumerable?>
{
    protected override object? Call(IEnumerable? arg)
    {
        if (arg is null)
            return null;
        return arg.Cast<object>().Max(Convert.ToDouble);
    }
}
