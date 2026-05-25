using Tinkwell.Expressions;
using Tinkwell.Expressions.Functions;
using Tinkwell.Measures.Functions;

namespace Tinkwell.Measures.Tests.Functions;

public class QuantityFunctionTests
{
    private static readonly IExpressionEvaluator Evaluator = new ExpressionEvaluator(
        ExpressionFunctionDiscovery.BuiltIn()
            .Concat(ExpressionFunctionDiscovery.FromAssemblyOf<QuantityFunction>())
            .ToList());

    [Fact]
    public async Task TwoArg_MillivoltsToBase()
    {
        var result = await Evaluator.EvaluateAsync("quantity(10, 'mV')");
        Assert.Equal(0.01, Assert.IsType<double>(result), 10);
    }

    [Fact]
    public async Task TwoArg_KilowattsToBase()
    {
        var result = await Evaluator.EvaluateAsync("quantity(2.5, 'kW')");
        Assert.Equal(2500.0, Assert.IsType<double>(result), 5);
    }

    [Fact]
    public async Task TwoArg_CelsiusToBase()
    {
        // SI base unit for Temperature is Kelvin
        var result = await Evaluator.EvaluateAsync("quantity(100, '°C')");
        Assert.Equal(373.15, Assert.IsType<double>(result), 5);
    }

    [Fact]
    public async Task ThreeArg_MillivoltsToKilovolts()
    {
        var result = await Evaluator.EvaluateAsync("quantity(10, 'mV', 'kV')");
        Assert.Equal(0.00001, Assert.IsType<double>(result), 15);
    }

    [Fact]
    public async Task ThreeArg_CelsiusToFahrenheit()
    {
        var result = await Evaluator.EvaluateAsync("quantity(100, '°C', '°F')");
        Assert.Equal(212.0, Assert.IsType<double>(result), 5);
    }

    [Fact]
    public async Task ThreeArg_MetersToMillimeters()
    {
        var result = await Evaluator.EvaluateAsync("quantity(1.5, 'm', 'mm')");
        Assert.Equal(1500.0, Assert.IsType<double>(result), 5);
    }

    [Fact]
    public async Task InExpression_AdditionWithQuantity()
    {
        // voltage is 230 V, add 10 mV converted to V
        var parameters = new Dictionary<string, object?> { ["voltage"] = 230.0 };
        var result = await Evaluator.EvaluateAsync(
            "voltage + quantity(10, 'mV')", parameters);
        Assert.Equal(230.01, Assert.IsType<double>(result), 10);
    }

    [Fact]
    public async Task InExpression_ComparisonWithQuantity()
    {
        var parameters = new Dictionary<string, object?> { ["temp"] = 90.0 };
        var result = await Evaluator.EvaluateAsync(
            "temp > quantity(80, '°C', '°C')", parameters);
        Assert.Equal(true, result);
    }

    [Fact]
    public async Task UnknownAbbreviation_Throws()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => Evaluator.EvaluateAsync("quantity(10, 'xyzzy')"));
    }

    [Fact]
    public async Task WrongArgCount_Throws()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => Evaluator.EvaluateAsync("quantity(10)"));
    }
}
