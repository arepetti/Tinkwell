using Tinkwell.Configuration;
using Tinkwell.Runlet.Coap.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Coap.Configuration.Tests;

public class CoapConfigParserTests
{
    private readonly CoapConfigParser _parser = new();

    private Task<CoapConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesMinimalConfig()
    {
        var config = await ParseFile("basic.tw");
        var server = Assert.Single(config.Servers);

        Assert.Equal("my-server", server.Name);
        Assert.Equal(5683, server.Port);

        var resource = Assert.Single(server.Resources);
        Assert.Equal("/sensor/+", resource.PathPattern);

        var verb = Assert.Single(resource.VerbBlocks);
        Assert.Equal("get", verb.Verb);
        Assert.Null(verb.WhenExpression);

        var binding = Assert.Single(verb.Bindings);
        Assert.Equal("measure", binding.BindingName);
        Assert.Equal("Tinkwell.Integrations", binding.AssemblyName);
        Assert.Null(binding.WhenExpression);
        Assert.True(binding.Properties.ContainsKey("name"));
        Assert.IsType<ExpressionValue>(binding.Properties["name"]);
    }

    [Fact]
    public async Task DefaultPort_UsesDefault5683()
    {
        var config = await ParseFile("default-port.tw");
        var server = Assert.Single(config.Servers);
        Assert.Equal(5683, server.Port);
    }

    [Fact]
    public async Task MultipleVerbs_AllParsed()
    {
        var config = await ParseFile("multiple-verbs.tw");
        var server = Assert.Single(config.Servers);
        Assert.Equal(5684, server.Port);

        var resource = Assert.Single(server.Resources);
        Assert.Equal(2, resource.VerbBlocks.Count);

        var getBlock = resource.VerbBlocks[0];
        Assert.Equal("get", getBlock.Verb);
        Assert.Single(getBlock.Bindings);

        var postBlock = resource.VerbBlocks[1];
        Assert.Equal("post", postBlock.Verb);
        Assert.Equal(2, postBlock.Bindings.Count);

        var eventBinding = postBlock.Bindings[1];
        Assert.Equal("event", eventBinding.BindingName);
        Assert.Equal("Tinkwell.Integrations", eventBinding.AssemblyName);
        Assert.True(eventBinding.Properties.ContainsKey("source"));
        Assert.True(eventBinding.Properties.ContainsKey("verb"));
        Assert.True(eventBinding.Properties.ContainsKey("name"));

        Assert.Single(eventBinding.NestedBlocks);
        Assert.True(eventBinding.NestedBlocks.ContainsKey("payload"));
        Assert.True(eventBinding.NestedBlocks["payload"].ContainsKey("device"));
    }

    [Fact]
    public async Task OnWhen_ParsesBlockLevelFilter()
    {
        var config = await ParseFile("on-when.tw");
        var resource = Assert.Single(config.Servers).Resources[0];
        var verbBlock = Assert.Single(resource.VerbBlocks);

        Assert.Equal("post", verbBlock.Verb);
        Assert.NotNull(verbBlock.WhenExpression);
        Assert.Contains("severity", verbBlock.WhenExpression);
        Assert.Contains("critical", verbBlock.WhenExpression);
    }

    [Fact]
    public async Task BindWhen_ParsesBindingLevelFilter()
    {
        var config = await ParseFile("bind-when.tw");
        var resource = Assert.Single(config.Servers).Resources[0];
        var verbBlock = Assert.Single(resource.VerbBlocks);

        Assert.Equal(2, verbBlock.Bindings.Count);

        Assert.Null(verbBlock.Bindings[0].WhenExpression);

        var conditionalBinding = verbBlock.Bindings[1];
        Assert.Equal("event", conditionalBinding.BindingName);
        Assert.NotNull(conditionalBinding.WhenExpression);
        Assert.Contains("alert", conditionalBinding.WhenExpression);
    }

    [Fact]
    public async Task StoreCrud_ParsesAllVerbs()
    {
        var config = await ParseFile("store-crud.tw");
        var resource = Assert.Single(config.Servers).Resources[0];

        Assert.Equal(4, resource.VerbBlocks.Count);

        var verbs = resource.VerbBlocks.Select(v => v.Verb).ToList();
        Assert.Contains("get", verbs);
        Assert.Contains("post", verbs);
        Assert.Contains("put", verbs);
        Assert.Contains("delete", verbs);

        var postBinding = resource.VerbBlocks.First(v => v.Verb == "post").Bindings[0];
        Assert.True(postBinding.Properties.ContainsKey("ttl"));
        var ttl = Assert.IsType<LongValue>(postBinding.Properties["ttl"]);
        Assert.Equal(3600, ttl.Value);
    }

    [Fact]
    public async Task MultipleResources_AllParsed()
    {
        var config = await ParseFile("multiple-resources.tw");
        var server = Assert.Single(config.Servers);

        Assert.Equal(2, server.Resources.Count);
        Assert.Equal("/sensor/+", server.Resources[0].PathPattern);
        Assert.Equal("/store/+", server.Resources[1].PathPattern);
    }

    [Fact]
    public async Task MultipleServers_AllParsed()
    {
        var config = await ParseFile("multiple-servers.tw");

        Assert.Equal(2, config.Servers.Count);
        Assert.Equal("sensors", config.Servers[0].Name);
        Assert.Equal(5683, config.Servers[0].Port);
        Assert.Equal("admin", config.Servers[1].Name);
        Assert.Equal(5684, config.Servers[1].Port);
    }

    [Fact]
    public async Task DuplicateServerName_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-server.tw"));
    }

    [Fact]
    public async Task MissingFrom_ParsesWithNullAssembly()
    {
        var config = await ParseFile("missing-from.tw");
        var server = Assert.Single(config.Servers);
        var resource = Assert.Single(server.Resources);
        var verb = Assert.Single(resource.VerbBlocks);
        var binding = Assert.Single(verb.Bindings);
        Assert.Equal("measure", binding.BindingName);
        Assert.Null(binding.AssemblyName);
    }

    [Fact]
    public async Task InvalidVerb_Throws()
    {
        await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-verb.tw"));
    }

    // -----------------------------------------------------------------------
    // on error: verb block level
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_VerbLevel_ParsesWithRetry()
    {
        var config = await ParseFile("on-error-verb.tw");
        var verb = config.Servers[0].Resources[0].VerbBlocks[0];

        Assert.NotNull(verb.OnError);
        Assert.Equal(ErrorPolicyAction.ResumeNext, verb.OnError!.Action);
        Assert.NotNull(verb.OnError.Retry);
        Assert.Equal(2, verb.OnError.Retry!.Count);
        Assert.Equal(500, verb.OnError.Retry.DelayMs);

        Assert.Null(verb.Bindings[0].OnError);
    }

    // -----------------------------------------------------------------------
    // on error: binding level
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnError_BindingLevel_ParsesStopThis()
    {
        var config = await ParseFile("on-error-binding.tw");
        var binding = config.Servers[0].Resources[0].VerbBlocks[0].Bindings[0];

        Assert.NotNull(binding.OnError);
        Assert.Equal(ErrorPolicyAction.StopThis, binding.OnError!.Action);
        Assert.Null(binding.OnError.Retry);
    }
}
