using Tinkwell.Configuration;
using Tinkwell.Runlet.ProtobufGateway.Configuration;

namespace Tinkwell.Runlet.ProtobufGateway.Configuration.Tests;

public sealed class ProtobufGatewayParserTests
{
    private readonly ProtobufGatewayParser _parser = new();

    private Task<ProtobufGatewayConfig> ParseFile(string relativePath)
    {
        var path = Path.Combine("TestFiles", relativePath);
        return _parser.LoadFileAsync(path);
    }

    [Fact]
    public async Task Basic_ParsesProfileWithModifiersAndRules()
    {
        var config = await ParseFile("basic.tw");
        var profile = Assert.Single(config.Gateways);

        Assert.Equal("device-fleet", profile.Name);
        Assert.Equal("pb", profile.Target);
        Assert.Equal("/{service}/{method}", profile.MatchPattern);
        Assert.Equal(2, profile.AllowRules.Count);
        Assert.Equal("tinkwell.measures.*", profile.AllowRules[0].ServicePattern);
        Assert.Equal("tinkwell.events.v1.EventBus", profile.AllowRules[1].ServicePattern);
    }

    [Fact]
    public async Task Defaults_OmittedForAndMatch_DefaultToStarAndStandardPath()
    {
        var config = await ParseFile("defaults.tw");
        var profile = Assert.Single(config.Gateways);

        Assert.Equal("open-access", profile.Name);
        Assert.Equal("*", profile.Target);
        Assert.Equal("/{service}/{method}", profile.MatchPattern);
        Assert.Single(profile.AllowRules);
        Assert.Equal("*", profile.AllowRules[0].ServicePattern);
    }

    [Fact]
    public async Task MultipleProfiles_ParsedInOrder()
    {
        var config = await ParseFile("multiple-profiles.tw");
        Assert.Equal(2, config.Gateways.Count);

        Assert.Equal("fleet", config.Gateways[0].Name);
        Assert.Equal("/device/{service}/{method}", config.Gateways[0].MatchPattern);
        Assert.Single(config.Gateways[0].AllowRules);

        Assert.Equal("admin", config.Gateways[1].Name);
        Assert.Equal("/admin/{service}/{method}", config.Gateways[1].MatchPattern);
        Assert.Single(config.Gateways[1].AllowRules);
    }

    [Fact]
    public async Task NoAllowRules_EmptyList()
    {
        var config = await ParseFile("no-allow.tw");
        var profile = Assert.Single(config.Gateways);

        Assert.Equal("locked-down", profile.Name);
        Assert.Empty(profile.AllowRules);
    }

    [Fact]
    public async Task InvalidMatch_MissingMethodPlaceholder_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("invalid-match.tw"));
        Assert.Contains("{method}", ex.Message);
    }

    [Fact]
    public async Task DuplicateName_Throws()
    {
        var ex = await Assert.ThrowsAsync<ConfigurationSyntaxException>(
            () => ParseFile("duplicate-name.tw"));
        Assert.Contains("Duplicate", ex.Message);
    }
}
