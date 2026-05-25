using Tinkwell.Cli.Commands.Init;

namespace Tinkwell.Cli.Commands.Init.Tests;

public class ConditionEvaluatorTests
{
    [Fact]
    public void TrueAnswer_IsTruthy()
    {
        var bag = new AnswerBag();
        bag.Set("events", true);
        Assert.True(bag.EvaluateCondition("events"));
    }

    [Fact]
    public void FalseAnswer_IsFalsy()
    {
        var bag = new AnswerBag();
        bag.Set("events", false);
        Assert.False(bag.EvaluateCondition("events"));
    }

    [Fact]
    public void MissingAnswer_IsFalsy()
    {
        var bag = new AnswerBag();
        Assert.False(bag.EvaluateCondition("events"));
    }

    [Fact]
    public void Negation_InvertsResult()
    {
        var bag = new AnswerBag();
        bag.Set("events", true);
        Assert.False(bag.EvaluateCondition("!events"));
    }

    [Fact]
    public void And_RequiresBothTrue()
    {
        var bag = new AnswerBag();
        bag.Set("measures", true);
        bag.Set("events", true);
        Assert.True(bag.EvaluateCondition("measures && events"));
    }

    [Fact]
    public void And_FalseWhenOneFalse()
    {
        var bag = new AnswerBag();
        bag.Set("measures", true);
        bag.Set("events", false);
        Assert.False(bag.EvaluateCondition("measures && events"));
    }

    [Fact]
    public void Or_TrueWhenEitherTrue()
    {
        var bag = new AnswerBag();
        bag.Set("coap", false);
        bag.Set("mqtt", true);
        Assert.True(bag.EvaluateCondition("coap || mqtt"));
    }

    [Fact]
    public void UnderscoreIdentifier_IsTruthy()
    {
        var bag = new AnswerBag();
        bag.Set("event_persistence", true);
        Assert.True(bag.EvaluateCondition("event_persistence"));
    }

    [Fact]
    public void UnderscoreIdentifier_IsFalsy()
    {
        var bag = new AnswerBag();
        bag.Set("event_persistence", false);
        Assert.False(bag.EvaluateCondition("event_persistence"));
    }

    [Fact]
    public void UnderscoreEquality_MatchesStringValue()
    {
        var bag = new AnswerBag();
        bag.Set("modbus_transport", "tcp");
        Assert.True(bag.EvaluateCondition("modbus_transport == 'tcp'"));
    }

    [Fact]
    public void UnderscoreInequality_RejectsMismatch()
    {
        var bag = new AnswerBag();
        bag.Set("modbus_transport", "rtu");
        Assert.True(bag.EvaluateCondition("modbus_transport != 'tcp'"));
    }

    [Fact]
    public void MixedBareAndUnderscore_Works()
    {
        var bag = new AnswerBag();
        bag.Set("modbus", true);
        bag.Set("modbus_transport", "tcp");
        Assert.True(bag.EvaluateCondition("modbus && modbus_transport == 'tcp'"));
    }

    [Fact]
    public void BracketedIdentifier_IsTruthy()
    {
        var bag = new AnswerBag();
        bag.Set("event-persistence", true);
        Assert.True(bag.EvaluateCondition("[event-persistence]"));
    }

    [Fact]
    public void BracketedEquality_MatchesStringValue()
    {
        var bag = new AnswerBag();
        bag.Set("modbus-transport", "tcp");
        Assert.True(bag.EvaluateCondition("[modbus-transport] == 'tcp'"));
    }

    [Fact]
    public void BracketedEquality_DoubleQuotes()
    {
        var bag = new AnswerBag();
        bag.Set("modbus-transport", "tcp");
        Assert.True(bag.EvaluateCondition("[modbus-transport] == \"tcp\""));
    }

    [Fact]
    public void ParenthesizedGroups_EvaluateCorrectly()
    {
        var bag = new AnswerBag();
        bag.Set("coap", true);
        bag.Set("measures", true);
        bag.Set("modbus", false);
        bag.Set("mqtt", true);

        Assert.True(bag.EvaluateCondition("coap && measures && (modbus || mqtt)"));
    }

    [Fact]
    public void NegatedParenGroup_Works()
    {
        var bag = new AnswerBag();
        bag.Set("measures", true);
        bag.Set("statemachines", false);
        Assert.True(bag.EvaluateCondition("measures && !statemachines"));
    }

    [Fact]
    public void ComplexExpression_WithUnderscores()
    {
        var bag = new AnswerBag();
        bag.Set("coap", true);
        bag.Set("measures", true);
        bag.Set("text_query", true);

        Assert.True(bag.EvaluateCondition("coap && measures && (modbus || text_query || i2c || mqtt)"));
    }

    [Fact]
    public void NullOrEmptyCondition_ReturnsTrue()
    {
        var bag = new AnswerBag();
        Assert.True(bag.EvaluateCondition(null));
        Assert.True(bag.EvaluateCondition(""));
        Assert.True(bag.EvaluateCondition("   "));
    }
}
