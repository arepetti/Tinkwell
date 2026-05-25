using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Configuration.Parser.Tests;

public class ErrorPolicyParserTests
{
    private static ConfigBlock MakeBlock(params Modifier[] modifiers) =>
        MakeBlock(modifiers, Array.Empty<Property>());

    private static ConfigBlock MakeBlock(Modifier[] modifiers, Property[] properties) =>
        new("on", "error", modifiers, properties, Array.Empty<ConfigBlock>(),
            new SourceLocation("test.tw", 1, 1));

    [Fact]
    public void ResumeNext_Parses()
    {
        var block = MakeBlock(new Modifier("resume", new StringValue("next")));
        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.ResumeNext, policy.Action);
        Assert.Null(policy.Retry);
        Assert.Null(policy.EventName);
        Assert.Null(policy.EventProperties);
    }

    [Fact]
    public void StopThis_Parses()
    {
        var block = MakeBlock(new Modifier("stop", new StringValue("this")));
        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.StopThis, policy.Action);
        Assert.Null(policy.Retry);
    }

    [Fact]
    public void StopApplication_Parses()
    {
        var block = MakeBlock(new Modifier("stop", new StringValue("application")));
        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.StopApplication, policy.Action);
        Assert.Null(policy.Retry);
    }

    [Fact]
    public void Publish_WithProperties_Parses()
    {
        var block = MakeBlock(
            [new Modifier("publish", new StringValue("handler-failure"))],
            [new Property("source", new StringValue("actions"), new SourceLocation("test.tw", 2, 5)),
             new Property("name", new StringValue("mqtt-failed"), new SourceLocation("test.tw", 3, 5))]);

        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.Publish, policy.Action);
        Assert.Equal("handler-failure", policy.EventName);
        Assert.NotNull(policy.EventProperties);
        Assert.Equal(2, policy.EventProperties!.Count);
        Assert.Equal("actions", ((StringValue)policy.EventProperties["source"]).Value);
        Assert.Equal("mqtt-failed", ((StringValue)policy.EventProperties["name"]).Value);
    }

    [Fact]
    public void RetryWithDefaults_Parses()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("retry", new LongValue(3)));

        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.ResumeNext, policy.Action);
        Assert.NotNull(policy.Retry);
        Assert.Equal(3, policy.Retry!.Count);
        Assert.Equal(1000, policy.Retry.DelayMs);
        Assert.Equal(1.0, policy.Retry.BackoffMultiplier);
    }

    [Fact]
    public void RetryWithDelayAndBackoff_Parses()
    {
        var block = MakeBlock(
            new Modifier("stop", new StringValue("this")),
            new Modifier("retry", new LongValue(5)),
            new Modifier("delay", new LongValue(500)),
            new Modifier("backoff", new LongValue(2)));

        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.StopThis, policy.Action);
        Assert.NotNull(policy.Retry);
        Assert.Equal(5, policy.Retry!.Count);
        Assert.Equal(500, policy.Retry.DelayMs);
        Assert.Equal(2.0, policy.Retry.BackoffMultiplier);
    }

    [Fact]
    public void RetryZero_NoRetryPolicy()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("retry", new LongValue(0)));

        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.ResumeNext, policy.Action);
        Assert.Null(policy.Retry);
    }

    [Fact]
    public void MissingAction_Throws()
    {
        var block = MakeBlock(new Modifier("retry", new LongValue(3)));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void DuplicateAction_Throws()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("stop", new StringValue("this")));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void UnknownModifier_Throws()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("unknown", new StringValue("value")));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void InvalidResumeValue_Throws()
    {
        var block = MakeBlock(new Modifier("resume", new StringValue("all")));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void InvalidStopValue_Throws()
    {
        var block = MakeBlock(new Modifier("stop", new StringValue("everything")));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void DelayWithoutRetry_Throws()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("delay", new LongValue(500)));

        Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
    }

    [Fact]
    public void Publish_WithoutProperties_Succeeds()
    {
        var block = MakeBlock(new Modifier("publish", new StringValue("evt")));
        var policy = ErrorPolicyParser.Parse(block);

        Assert.Equal(ErrorPolicyAction.Publish, policy.Action);
        Assert.Equal("evt", policy.EventName);
        Assert.Null(policy.EventProperties);
    }

    [Fact]
    public void NegativeRetry_Throws()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("retry", new LongValue(-1)));

        var ex = Assert.Throws<ConfigurationSyntaxException>(
            () => ErrorPolicyParser.Parse(block));
        Assert.Contains("non-negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetryWithBackoffOnly_UsesDefaultDelay()
    {
        var block = MakeBlock(
            new Modifier("stop", new StringValue("this")),
            new Modifier("retry", new LongValue(2)),
            new Modifier("backoff", new LongValue(3)));

        var policy = ErrorPolicyParser.Parse(block);

        Assert.NotNull(policy.Retry);
        Assert.Equal(2, policy.Retry!.Count);
        Assert.Equal(1000, policy.Retry.DelayMs);
        Assert.Equal(3.0, policy.Retry.BackoffMultiplier);
    }

    [Fact]
    public void DuplicateRetryModifier_LastValueWins()
    {
        var block = MakeBlock(
            new Modifier("resume", new StringValue("next")),
            new Modifier("retry", new LongValue(1)),
            new Modifier("retry", new LongValue(4)));

        var policy = ErrorPolicyParser.Parse(block);

        Assert.NotNull(policy.Retry);
        Assert.Equal(4, policy.Retry!.Count);
    }
}
