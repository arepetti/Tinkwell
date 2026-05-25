using Tinkwell.Expressions;

namespace Tinkwell.Expressions.Tests;

public class DependencyWalkerTests
{
    private sealed record Item(string Name, string? Expression);

    private static readonly DependencyWalker<Item> Walker = new(
        item => item.Name,
        item => item.Expression);

    [Fact]
    public void NoDependencies_ReturnsAllItems()
    {
        var items = new[]
        {
            new Item("a", null),
            new Item("b", null),
        };

        var result = Walker.Analyze(items);

        Assert.Equal(2, result.CalculationOrder.Count);
        Assert.All(result.ForwardDependencies.Values, deps => Assert.Empty(deps));
        Assert.Empty(result.ReverseDependencies);
    }

    [Fact]
    public void LinearChain_CorrectOrder()
    {
        var items = new[]
        {
            new Item("c", "a + b"),
            new Item("b", "a * 2"),
            new Item("a", null),
        };

        var result = Walker.Analyze(items);

        var names = result.CalculationOrder.Select(i => i.Name).ToList();
        Assert.True(names.IndexOf("a") < names.IndexOf("b"));
        Assert.True(names.IndexOf("b") < names.IndexOf("c"));
    }

    [Fact]
    public void DiamondDependency_CorrectOrder()
    {
        // d depends on b and c; both b and c depend on a
        var items = new[]
        {
            new Item("d", "b + c"),
            new Item("c", "a * 3"),
            new Item("b", "a * 2"),
            new Item("a", null),
        };

        var result = Walker.Analyze(items);

        var names = result.CalculationOrder.Select(i => i.Name).ToList();
        Assert.True(names.IndexOf("a") < names.IndexOf("b"));
        Assert.True(names.IndexOf("a") < names.IndexOf("c"));
        Assert.True(names.IndexOf("b") < names.IndexOf("d"));
        Assert.True(names.IndexOf("c") < names.IndexOf("d"));
    }

    [Fact]
    public void Analyze_DuplicateNames_ThrowsArgumentException()
    {
        var items = new[]
        {
            new Item("same", "1"),
            new Item("same", "2"),
        };

        var ex = Assert.Throws<ArgumentException>(() => Walker.Analyze(items));
        Assert.Equal("items", ex.ParamName);
    }

    [Fact]
    public void CircularDependency_Throws()
    {
        var items = new[]
        {
            new Item("a", "b + 1"),
            new Item("b", "a + 1"),
        };

        var ex = Assert.Throws<CircularDependencyException>(() => Walker.Analyze(items));

        Assert.Contains("a", ex.CycleParticipants);
        Assert.Contains("b", ex.CycleParticipants);
    }

    [Fact]
    public void SelfReference_Throws()
    {
        var items = new[]
        {
            new Item("x", "x + 1"),
        };

        Assert.Throws<CircularDependencyException>(() => Walker.Analyze(items));
    }

    [Fact]
    public void ExternalParameters_IncludedInDependencies()
    {
        var items = new[]
        {
            new Item("result", "external_param + local"),
            new Item("local", null),
        };

        var result = Walker.Analyze(items);

        var resultDeps = result.ForwardDependencies["result"];
        Assert.Contains("external_param", resultDeps);
        Assert.Contains("local", resultDeps);
        Assert.Contains("result", result.ReverseDependencies["external_param"]);
    }

    [Fact]
    public void SourceMeasures_InReverseDeps()
    {
        // Only derived items passed, but source measures A/B must
        // appear in ReverseDependencies so that changes to A or B
        // trigger recalculation of C (and cascade to D).
        var items = new[]
        {
            new Item("C", "A + B"),
            new Item("D", "C + 1"),
        };

        var result = Walker.Analyze(items);

        Assert.Contains("C", result.ReverseDependencies["A"]);
        Assert.Contains("C", result.ReverseDependencies["B"]);
        Assert.Contains("D", result.ReverseDependencies["C"]);

        var forwardC = result.ForwardDependencies["C"];
        Assert.Contains("A", forwardC);
        Assert.Contains("B", forwardC);

        var names = result.CalculationOrder.Select(i => i.Name).ToList();
        Assert.True(names.IndexOf("C") < names.IndexOf("D"));
    }

    [Fact]
    public void ForwardDependencies_Populated()
    {
        var items = new[]
        {
            new Item("power", "voltage * current"),
            new Item("voltage", null),
            new Item("current", null),
        };

        var result = Walker.Analyze(items);

        var powerDeps = result.ForwardDependencies["power"];
        Assert.Contains("voltage", powerDeps);
        Assert.Contains("current", powerDeps);
        Assert.Empty(result.ForwardDependencies["voltage"]);
        Assert.Empty(result.ForwardDependencies["current"]);
    }

    [Fact]
    public void ReverseDependencies_Populated()
    {
        var items = new[]
        {
            new Item("power", "voltage * current"),
            new Item("voltage", null),
            new Item("current", null),
        };

        var result = Walker.Analyze(items);

        Assert.Contains("power", result.ReverseDependencies["voltage"]);
        Assert.Contains("power", result.ReverseDependencies["current"]);
    }

    [Fact]
    public void ExtractParameters_ReturnsIdentifiers()
    {
        var names = DependencyWalker<Item>.ExtractParameters("a + b * c");

        Assert.Contains("a", names);
        Assert.Contains("b", names);
        Assert.Contains("c", names);
    }

    [Fact]
    public void ExtractParameters_IgnoresNumericLiterals()
    {
        var names = DependencyWalker<Item>.ExtractParameters("x + 42");

        Assert.Contains("x", names);
        Assert.DoesNotContain("42", names);
    }

    [Fact]
    public void ExtractParameters_BracketedNameWithSpace()
    {
        var names = DependencyWalker<Item>.ExtractParameters("[my var] + x");
        Assert.Contains("my var", names);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var result = Walker.Analyze(Array.Empty<Item>());
        Assert.Empty(result.CalculationOrder);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("")]
    public void WhitespaceOrEmptyExpression_NoForwardDeps(string? expr)
    {
        var items = new[] { new Item("x", expr) };
        var result = Walker.Analyze(items);
        Assert.Empty(result.ForwardDependencies["x"]);
        Assert.Single(result.CalculationOrder);
    }

    [Fact]
    public void ThreeNodeCycle_Throws_CycleSupersetInMessage()
    {
        // a -> b -> c -> a
        var items = new[]
        {
            new Item("a", "b + 1"),
            new Item("b", "c + 1"),
            new Item("c", "a + 1"),
        };
        var ex = Assert.Throws<CircularDependencyException>(() => Walker.Analyze(items));
        Assert.True(ex.CycleParticipants.Count >= 2);
    }

    [Fact]
    public void CycleWithIndependentAndDownstream_F4_ParticipantSuperset()
    {
        // Cycle among a,b,c; w is independent; d depends on a (downstream) — d appears in the unsatisfied set (F4).
        var items = new[]
        {
            new Item("a", "b + 1"),
            new Item("b", "c + 1"),
            new Item("c", "a + 1"),
            new Item("w", null),
            new Item("d", "a + 1"),
        };
        var ex = Assert.Throws<CircularDependencyException>(() => Walker.Analyze(items));
        Assert.Contains("a", ex.CycleParticipants);
        Assert.Contains("d", ex.CycleParticipants);
        Assert.NotNull(ex.Message);
    }
}
