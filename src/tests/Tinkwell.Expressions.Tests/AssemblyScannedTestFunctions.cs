using NCalc.Handlers;
using Tinkwell.Expressions.Functions;

namespace Tinkwell.Expressions.Tests;

/// <summary>Marker type for <see cref="ExpressionFunctionDiscovery.FromAssembly(Assembly)"/> of the test assembly (ordered after "alpha" by name).</summary>
public sealed class ZebraScanFunction : IExpressionFunction
{
    public string Name => "zebra";

    public object? Invoke(FunctionArgs args) => 1;
}

/// <summary>Discovered with <c>FromAssembly</c> of the test assembly; sorts before "zebra" by name.</summary>
public sealed class AlphaScanFunction : IExpressionFunction
{
    public string Name => "alpha";

    public object? Invoke(FunctionArgs args) => 2;
}
