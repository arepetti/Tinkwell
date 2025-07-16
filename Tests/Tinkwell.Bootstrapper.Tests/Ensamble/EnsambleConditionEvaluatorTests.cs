using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.Bootstrapper.Tests.Ensamble;

public class EnsambleConditionEvaluatorTests
{
    private class TestConditionalDefinition : IConditionalDefinition
    {
        public string? Condition { get; set; }
    }

    [Fact]
    public void Filter_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ensamble:Params:MyParam", "MyValue" }
            })
            .Build();

        var evaluator = new EnsambleConditionEvaluator(config, new ExpressionEvaluator());

        var definitions = new[]
        {
            new TestConditionalDefinition { Condition = "MyParam == 'MyValue'" },
            new TestConditionalDefinition { Condition = "MyParam == 'WrongValue'" }
        };

        var filtered = evaluator.Filter(definitions).ToList();

        Assert.Single(filtered);
        Assert.Equal("MyParam == 'MyValue'", filtered[0].Condition);
    }
}
