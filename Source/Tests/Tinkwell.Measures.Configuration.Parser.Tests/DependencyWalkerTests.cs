namespace Tinkwell.Measures.Configuration.Parser.Tests;

public class DependencyWalkerTests
{
    private class TestMeasure : IMeasureDependent
    {
        public string Name { get; set; } = string.Empty;
        public string Expression { get; set; } = string.Empty;
        public IList<string> Dependencies { get; } = new List<string>();
    }

    [Fact]
    public void DependencyWalker_AnalyzesSimpleDependencies()
    {
        var measureA = new TestMeasure { Name = "A", Expression = "1 + B" };
        var measureB = new TestMeasure { Name = "B", Expression = "2" };
        var measureC = new TestMeasure { Name = "C", Expression = "A + B" };

        var measures = new List<TestMeasure> { measureA, measureB, measureC };

        var walker = new DependencyWalker<TestMeasure>();
        var result = walker.Analyze(measures);

        Assert.True(result);
        Assert.Equal(3, walker.CalculationOrder.Count);
        Assert.Contains("B", walker.CalculationOrder);
        Assert.Contains("A", walker.CalculationOrder);
        Assert.Contains("C", walker.CalculationOrder);

        // Verify the order: B must come before A, and both B and A must come before C
        var calculationOrderList = walker.CalculationOrder.ToList();
        Assert.True(calculationOrderList.IndexOf("B") < calculationOrderList.IndexOf("A"));
        Assert.True(calculationOrderList.IndexOf("B") < calculationOrderList.IndexOf("C"));
        Assert.True(calculationOrderList.IndexOf("A") < calculationOrderList.IndexOf("C"));

        // Verify ForwardDependencyMap
        Assert.Single(walker.ForwardDependencyMap["A"]);
        Assert.Contains("B", walker.ForwardDependencyMap["A"]);
        Assert.Empty(walker.ForwardDependencyMap["B"]);
        Assert.Equal(2, walker.ForwardDependencyMap["C"].Count);
        Assert.Contains("A", walker.ForwardDependencyMap["C"]);
        Assert.Contains("B", walker.ForwardDependencyMap["C"]);

        // Verify ReverseDependencyMap
        Assert.Single(walker.ReverseDependencyMap["A"]);
        Assert.Contains("C", walker.ReverseDependencyMap["A"]);
        Assert.Equal(2, walker.ReverseDependencyMap["B"].Count);
        Assert.Contains("A", walker.ReverseDependencyMap["B"]);
        Assert.Contains("C", walker.ReverseDependencyMap["B"]);

        // Verify IMeasureDependent.Dependencies
        Assert.Single(measureA.Dependencies);
        Assert.Contains("B", measureA.Dependencies);
        Assert.Empty(measureB.Dependencies);
        Assert.Equal(2, measureC.Dependencies.Count);
        Assert.Contains("A", measureC.Dependencies);
        Assert.Contains("B", measureC.Dependencies);
    }

    [Fact]
    public void DependencyWalker_DetectsCircularDependencies()
    {
        var measureX = new TestMeasure { Name = "X", Expression = "Y" };
        var measureY = new TestMeasure { Name = "Y", Expression = "X" };

        var measures = new List<TestMeasure> { measureX, measureY };

        var walker = new DependencyWalker<TestMeasure>();
        var result = walker.Analyze(measures);

        Assert.False(result);
        Assert.Empty(walker.CalculationOrder);
    }
}