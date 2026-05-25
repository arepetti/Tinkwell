using System.Net;
using Tinkwell.Lwm2m;
using Tinkwell.Lwm2m.Registration;

namespace Tinkwell.Lwm2m.Tests;

public class RegistrationParserTests
{
    private static readonly IPEndPoint TestEndpoint = new(IPAddress.Loopback, 5683);

    [Fact]
    public void Parse_WithAllQueryParams_ExtractsCorrectly()
    {
        var reg = RegistrationParser.Parse(
            "ep=device1&lt=300&lwm2m=1.1&b=U",
            "</3/0>,</3303/0>,</3304/0>",
            TestEndpoint);

        Assert.Equal("device1", reg.Endpoint);
        Assert.Equal(300, reg.Lifetime);
        Assert.Equal("1.1", reg.LwM2MVersion);
        Assert.Equal("U", reg.BindingMode);
    }

    [Fact]
    public void Parse_MinimalQuery_UsesDefaults()
    {
        var reg = RegistrationParser.Parse("ep=sensor1", null, TestEndpoint);

        Assert.Equal("sensor1", reg.Endpoint);
        Assert.Equal(RegistrationParser.DefaultLifetimeSeconds, reg.Lifetime);
        Assert.Null(reg.LwM2MVersion);
    }

    [Fact]
    public void Parse_NoEndpoint_FallsBackToRemoteAddress()
    {
        var reg = RegistrationParser.Parse(null, null, TestEndpoint);
        Assert.Equal(TestEndpoint.ToString(), reg.Endpoint);
    }

    [Fact]
    public void ParseLinkFormat_ValidLinks_ExtractsPaths()
    {
        var links = "</3/0>,</3303/0>,</3304/0>,</3304/1>";
        var paths = RegistrationParser.ParseLinkFormat(links);

        Assert.Equal(4, paths.Count);
        Assert.Equal(new Lwm2mPath(3, 0), paths[0]);
        Assert.Equal(new Lwm2mPath(3303, 0), paths[1]);
        Assert.Equal(new Lwm2mPath(3304, 0), paths[2]);
        Assert.Equal(new Lwm2mPath(3304, 1), paths[3]);
    }

    [Fact]
    public void ParseLinkFormat_EmptyPayload_ReturnsEmptyList()
    {
        Assert.Empty(RegistrationParser.ParseLinkFormat(null));
        Assert.Empty(RegistrationParser.ParseLinkFormat(""));
    }

    [Fact]
    public void ParseLinkFormat_LinkWithAttributes_IgnoresAttributes()
    {
        var links = "</3303/0>;rt=\"urn:oma:lwm2m:ext:3303\"";
        var paths = RegistrationParser.ParseLinkFormat(links);

        Assert.Single(paths);
        Assert.Equal(new Lwm2mPath(3303, 0), paths[0]);
    }

    [Fact]
    public void ParseLinkFormat_NonNumericPath_Skipped()
    {
        var links = "</3303/0>,</bs>,</rd>";
        var paths = RegistrationParser.ParseLinkFormat(links);

        Assert.Single(paths);
        Assert.Equal(new Lwm2mPath(3303, 0), paths[0]);
    }

    [Fact]
    public void ParseQueryParameters_HandlesUrlEncoding()
    {
        var result = RegistrationParser.ParseQueryParameters("ep=my%20device&lt=600");
        Assert.Equal("my device", result["ep"]);
        Assert.Equal("600", result["lt"]);
    }

    [Fact]
    public void ParseQueryParameters_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(RegistrationParser.ParseQueryParameters(null));
        Assert.Empty(RegistrationParser.ParseQueryParameters(""));
    }

    [Fact]
    public void ParseQueryParameters_DuplicateKeys_LastWins()
    {
        var result = RegistrationParser.ParseQueryParameters("lt=1&lt=999&ep=first&ep=second");
        Assert.Equal("999", result["lt"]);
        Assert.Equal("second", result["ep"]);
    }

    [Fact]
    public void ParseQueryParameters_SegmentWithoutEquals_IsSkipped()
    {
        var result = RegistrationParser.ParseQueryParameters("b&ep=device&lt=60");
        Assert.Equal(2, result.Count);
        Assert.Equal("device", result["ep"]);
        Assert.Equal("60", result["lt"]);
    }

    [Fact]
    public void ParseQueryParameters_EmptyValueAfterKey_PreservesKeyWithEmptyString()
    {
        var result = RegistrationParser.ParseQueryParameters("ep=");
        Assert.Single(result);
        Assert.Equal("", result["ep"]);
    }

    [Fact]
    public void Parse_AllLinksInvalid_ProducesEmptyObjectList()
    {
        var reg = RegistrationParser.Parse("ep=x", ",,</foo>,</notnumeric>", TestEndpoint);
        Assert.Empty(reg.Objects);
    }

    [Fact]
    public void Parse_LtZero_SetsZeroLifetime()
    {
        var reg = RegistrationParser.Parse("ep=x&lt=0", null, TestEndpoint);
        Assert.Equal(0, reg.Lifetime);
    }

    [Fact]
    public void Parse_LtNegative_SetsNegativeLifetime()
    {
        var reg = RegistrationParser.Parse("ep=x&lt=-1", null, TestEndpoint);
        Assert.Equal(-1, reg.Lifetime);
    }

    [Fact]
    public void Parse_LtEmpty_FallsBackToDefaultLifetime()
    {
        var reg = RegistrationParser.Parse("ep=x&lt=", null, TestEndpoint);
        Assert.Equal(RegistrationParser.DefaultLifetimeSeconds, reg.Lifetime);
    }
}
