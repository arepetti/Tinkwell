using Tinkwell.Configuration;
using Tinkwell.Runlet.Mqtt.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Mqtt.Configuration.Tests;

public class MqttConfigParserTests
{
    private readonly MqttConfigParser _parser = new();

    private Task<MqttConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesMinimalConfig()
    {
        var config = await ParseFile("basic.tw");
        var conn = Assert.Single(config.Connections);

        Assert.Equal("sensors", conn.Name);
        Assert.Equal("localhost", conn.Broker);
        Assert.Equal(1883, conn.Port);
        Assert.Equal("tinkwell", conn.ClientId);
        Assert.Null(conn.Username);
        Assert.Null(conn.Password);
        Assert.Equal(3, conn.RetryCount);
        Assert.Equal(2000, conn.RetryDelay);

        var sub = Assert.Single(conn.Subscriptions);
        Assert.Equal("sensor/+", sub.TopicFilter);
        var verbBlock = Assert.Single(sub.VerbBlocks);
        Assert.Equal("message", verbBlock.Verb);
        var bindRef = Assert.Single(verbBlock.Bindings);
        Assert.Equal("event", bindRef.BindingName);
        Assert.Equal("Tinkwell.Integrations", bindRef.AssemblyName);
        Assert.True(bindRef.Properties.ContainsKey("source"));
        Assert.True(bindRef.Properties.ContainsKey("verb"));
        Assert.True(bindRef.Properties.ContainsKey("name"));
        Assert.True(bindRef.Properties.ContainsKey("object"));
    }

    [Fact]
    public async Task FullSettings_ParsesAllConnectionProperties()
    {
        var config = await ParseFile("full-settings.tw");
        var conn = Assert.Single(config.Connections);

        Assert.Equal("factory-floor", conn.Name);
        Assert.Equal("192.168.1.100", conn.Broker);
        Assert.Equal(8883, conn.Port);
        Assert.Equal("tinkwell-factory", conn.ClientId);
        Assert.Equal("admin", conn.Username);
        Assert.Equal("secret", conn.Password);
        Assert.Equal(5, conn.RetryCount);
        Assert.Equal(5000, conn.RetryDelay);

        var sub = Assert.Single(conn.Subscriptions);
        Assert.Equal("sensor/+", sub.TopicFilter);
        var bindRef = Assert.Single(Assert.Single(sub.VerbBlocks).Bindings);
        Assert.IsType<StringValue>(bindRef.Properties["source"]);
        Assert.IsType<StringValue>(bindRef.Properties["verb"]);
        Assert.IsType<ExpressionValue>(bindRef.Properties["name"]);
        Assert.IsType<ExpressionValue>(bindRef.Properties["object"]);
    }

    [Fact]
    public async Task MultipleSubscriptions_AllParsed()
    {
        var config = await ParseFile("multiple-subscriptions.tw");
        var conn = Assert.Single(config.Connections);

        Assert.Equal(2, conn.Subscriptions.Count);

        Assert.Equal("sensor/+", conn.Subscriptions[0].TopicFilter);
        var bind0 = Assert.Single(Assert.Single(conn.Subscriptions[0].VerbBlocks).Bindings);
        Assert.IsType<ExpressionValue>(bind0.Properties["name"]);
        Assert.IsType<ExpressionValue>(bind0.Properties["object"]);

        Assert.Equal("alerts/#", conn.Subscriptions[1].TopicFilter);
        var bind1 = Assert.Single(Assert.Single(conn.Subscriptions[1].VerbBlocks).Bindings);
        var verb = Assert.IsType<StringValue>(bind1.Properties["verb"]);
        Assert.Equal("fired", verb.Value);
    }

    [Fact]
    public async Task PayloadProperties_ParsedFromWithBlock()
    {
        var config = await ParseFile("payload-properties.tw");
        var sub = Assert.Single(config.Connections).Subscriptions[0];
        var bindRef = Assert.Single(Assert.Single(sub.VerbBlocks).Bindings);

        Assert.True(bindRef.NestedBlocks.ContainsKey("payload"));
        var payloadBlock = bindRef.NestedBlocks["payload"];
        Assert.Equal(2, payloadBlock.Count);
        Assert.True(payloadBlock.ContainsKey("unit"));
        Assert.True(payloadBlock.ContainsKey("device"));
        Assert.IsType<ExpressionValue>(payloadBlock["unit"]);
        Assert.IsType<ExpressionValue>(payloadBlock["device"]);
    }

    [Fact]
    public async Task MultipleConnections_AllParsed()
    {
        var config = await ParseFile("multiple-connections.tw");

        Assert.Equal(2, config.Connections.Count);
        Assert.Equal("warehouse", config.Connections[0].Name);
        Assert.Equal("broker-a.local", config.Connections[0].Broker);
        Assert.Equal("factory", config.Connections[1].Name);
        Assert.Equal("broker-b.local", config.Connections[1].Broker);
        Assert.Equal(8883, config.Connections[1].Port);
    }

    [Fact]
    public async Task DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-name.tw"));
    }

    [Fact]
    public async Task MissingBroker_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("missing-broker.tw"));
    }

    [Fact]
    public async Task EmptySubscribe_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("empty-subscribe.tw"));
    }

    [Fact]
    public async Task BindWithoutFrom_ParsesWithNullAssembly()
    {
        var config = await ParseFile("bind-without-from.tw");
        var conn = Assert.Single(config.Connections);
        var sub = Assert.Single(conn.Subscriptions);
        var verb = Assert.Single(sub.VerbBlocks);
        var binding = Assert.Single(verb.Bindings);
        Assert.Equal("event", binding.BindingName);
        Assert.Null(binding.AssemblyName);
    }

    // -----------------------------------------------------------------------
    // on error: verb block level
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_VerbLevel_ParsesStopApplication()
    {
        var config = await ParseFile("on-error-verb.tw");
        var verb = config.Connections[0].Subscriptions[0].VerbBlocks[0];

        Assert.NotNull(verb.OnError);
        Assert.Equal(ErrorPolicyAction.StopApplication, verb.OnError!.Action);
        Assert.Null(verb.OnError.Retry);

        Assert.Null(verb.Bindings[0].OnError);
    }

    // -----------------------------------------------------------------------
    // on error: binding level with retry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_BindingLevel_ParsesWithRetry()
    {
        var config = await ParseFile("on-error-binding.tw");
        var binding = config.Connections[0].Subscriptions[0].VerbBlocks[0].Bindings[0];

        Assert.NotNull(binding.OnError);
        Assert.Equal(ErrorPolicyAction.ResumeNext, binding.OnError!.Action);
        Assert.NotNull(binding.OnError.Retry);
        Assert.Equal(3, binding.OnError.Retry!.Count);
        Assert.Equal(1000, binding.OnError.Retry.DelayMs);
        Assert.Equal(2.0, binding.OnError.Retry.BackoffMultiplier);
    }
}
