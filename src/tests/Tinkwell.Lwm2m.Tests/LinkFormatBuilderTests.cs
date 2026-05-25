using Tinkwell.Lwm2m.Registration;

namespace Tinkwell.Lwm2m.Tests;

public class LinkFormatBuilderTests
{
    [Fact]
    public void BuildRegistrationPayload_MultiplePaths_JoinsWithCommas()
    {
        var payload = LinkFormatBuilder.BuildRegistrationPayload(new[] { "3/0", "3303/0", "3304/0" });
        Assert.Equal("</3/0>,</3303/0>,</3304/0>", payload);
    }

    [Theory]
    [InlineData("3/0", "/3/0")]
    [InlineData("/3/0", "/3/0")]
    [InlineData("////3/0", "/3/0")]
    public void BuildRegistrationPayload_LeadingSlashes_TrimmedToSingleSlashForm(string path, string linkBody)
    {
        var expected = $"<{linkBody}>";
        var payload = LinkFormatBuilder.BuildRegistrationPayload(new[] { path });
        Assert.Equal(expected, payload);
    }

    [Fact]
    public void BuildRegistrationPayload_EmptyCollection_ReturnsEmptyString()
    {
        Assert.Equal("", LinkFormatBuilder.BuildRegistrationPayload(Array.Empty<string>()));
        Assert.Equal("", LinkFormatBuilder.BuildRegistrationPayload(Enumerable.Empty<string>()));
    }

    [Fact]
    public void BuildRegistrationPayload_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LinkFormatBuilder.BuildRegistrationPayload(null!));
    }

    [Fact]
    public void BuildRegistrationPayload_SinglePath_FormatsAsSingleLink()
    {
        var payload = LinkFormatBuilder.BuildRegistrationPayload(new[] { "3303/0" });
        Assert.Equal("</3303/0>", payload);
    }

    [Fact]
    public void BuildRegistrationPayload_MixedWithAndWithoutSlashes_ProducesValidLinks()
    {
        var payload = LinkFormatBuilder.BuildRegistrationPayload(new[] { "3/0", "/3303/0" });
        Assert.Equal("</3/0>,</3303/0>", payload);
    }

    [Fact]
    public void BuildRegistrationPayload_RoundTrip_ParseLinkFormat_RecoversPaths()
    {
        var paths = new[] { "3/0", "/3303/0", "3304/1" };
        var built = LinkFormatBuilder.BuildRegistrationPayload(paths);
        var parsed = RegistrationParser.ParseLinkFormat(built);

        Assert.Equal(3, parsed.Count);
        Assert.Equal(new Lwm2mPath(3, 0), parsed[0]);
        Assert.Equal(new Lwm2mPath(3303, 0), parsed[1]);
        Assert.Equal(new Lwm2mPath(3304, 1), parsed[2]);
    }
}
