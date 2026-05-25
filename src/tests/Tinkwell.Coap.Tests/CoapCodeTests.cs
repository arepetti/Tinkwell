using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapCodeTests
{
    [Theory]
    [InlineData(CoapCode.Get, "GET")]
    [InlineData(CoapCode.Post, "POST")]
    [InlineData(CoapCode.Put, "PUT")]
    [InlineData(CoapCode.Delete, "DELETE")]
    public void ToMethodString_KnownCodes(byte code, string expected)
    {
        Assert.Equal(expected, CoapCode.ToMethodString(code));
    }

    [Fact]
    public void ToMethodString_UnknownCode_FormatsAsDecimal()
    {
        var result = CoapCode.ToMethodString(0xFF);
        Assert.StartsWith("0.", result);
    }

    [Theory]
    [InlineData(CoapCode.Content, "2.05")]
    [InlineData(CoapCode.Continue, "2.31")]
    [InlineData(CoapCode.NotFound, "4.04")]
    [InlineData(CoapCode.MethodNotAllowed, "4.05")]
    [InlineData(CoapCode.RequestEntityIncomplete, "4.08")]
    [InlineData(CoapCode.RequestEntityTooLarge, "4.13")]
    [InlineData(CoapCode.InternalServerError, "5.00")]
    [InlineData(CoapCode.Created, "2.01")]
    [InlineData(CoapCode.Changed, "2.04")]
    [InlineData(CoapCode.BadRequest, "4.00")]
    [InlineData(CoapCode.Valid, "2.03")]
    [InlineData(CoapCode.PreconditionFailed, "4.12")]
    [InlineData(CoapCode.UnsupportedContentFormat, "4.15")]
    public void ToDisplayString_FormatsCclassDetail(byte code, string expected)
    {
        Assert.Equal(expected, CoapCode.ToDisplayString(code));
    }

    // RFC 7252, Section 12.1.2 maps c.dd to a single byte as ((c << 5) | dd). Pin the new
    // codes' numeric values so a future renaming or accidental refactor cannot shift the wire
    // representation underneath callers that compare against CoapCode.* constants.
    [Fact]
    public void NewCodes_HaveExpectedNumericValues()
    {
        Assert.Equal(0x43, CoapCode.Valid);
        Assert.Equal(0x8C, CoapCode.PreconditionFailed);
        Assert.Equal(0x8F, CoapCode.UnsupportedContentFormat);
    }

    [Fact]
    public void NewCodes_AreInExpectedClasses()
    {
        Assert.Equal(2, CoapCode.Valid >> 5);
        Assert.Equal(4, CoapCode.PreconditionFailed >> 5);
        Assert.Equal(4, CoapCode.UnsupportedContentFormat >> 5);
    }

    [Fact]
    public void RequestCodes_AreInClass0()
    {
        Assert.Equal(0, CoapCode.Get >> 5);
        Assert.Equal(0, CoapCode.Post >> 5);
        Assert.Equal(0, CoapCode.Put >> 5);
        Assert.Equal(0, CoapCode.Delete >> 5);
    }

    [Fact]
    public void SuccessCodes_AreInClass2()
    {
        Assert.Equal(2, CoapCode.Created >> 5);
        Assert.Equal(2, CoapCode.Deleted >> 5);
        Assert.Equal(2, CoapCode.Changed >> 5);
        Assert.Equal(2, CoapCode.Content >> 5);
        Assert.Equal(2, CoapCode.Continue >> 5);
    }

    [Fact]
    public void ClientErrorCodes_AreInClass4()
    {
        Assert.Equal(4, CoapCode.BadRequest >> 5);
        Assert.Equal(4, CoapCode.NotFound >> 5);
        Assert.Equal(4, CoapCode.MethodNotAllowed >> 5);
        Assert.Equal(4, CoapCode.RequestEntityIncomplete >> 5);
        Assert.Equal(4, CoapCode.RequestEntityTooLarge >> 5);
    }

    [Fact]
    public void ServerErrorCodes_AreInClass5()
    {
        Assert.Equal(5, CoapCode.InternalServerError >> 5);
    }
}
