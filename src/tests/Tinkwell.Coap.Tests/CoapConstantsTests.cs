using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapConstantsTests
{
    [Fact]
    public void Version_Is1()
    {
        Assert.Equal(1, CoapConstants.Version);
    }

    [Fact]
    public void MinHeaderSize_Is4()
    {
        Assert.Equal(4, CoapConstants.MinHeaderSize);
    }

    [Fact]
    public void MaxTokenLength_Is8()
    {
        Assert.Equal(8, CoapConstants.MaxTokenLength);
    }

    [Fact]
    public void PayloadMarker_Is0xFF()
    {
        Assert.Equal(0xFF, CoapConstants.PayloadMarker);
    }

    [Fact]
    public void ObserveValues()
    {
        Assert.Equal(0, CoapConstants.ObserveRegister);
        Assert.Equal(1, CoapConstants.ObserveDeregister);
    }

    [Fact]
    public void ObserveSequenceMask_Is24Bits()
    {
        Assert.Equal(0xFFFFFF, CoapConstants.ObserveSequenceMask);
    }

    [Fact]
    public void HardCeilings_PinExpectedValues()
    {
        // Hard ceilings must be stable across releases: callers configure their parse limits
        // against these and the values are documented in the public README.
        Assert.Equal(65535, CoapConstants.MaxMessageSizeCeiling);
        Assert.Equal(1024, CoapConstants.MaxOptionCountCeiling);
        Assert.Equal(65535, CoapConstants.MaxOptionValueLengthCeiling);
    }

    [Fact]
    public void ContentFormat_WellKnownValues()
    {
        Assert.Equal(0, (int)CoapContentFormat.TextPlain);
        Assert.Equal(42, (int)CoapContentFormat.ApplicationOctetStream);
        Assert.Equal(50, (int)CoapContentFormat.ApplicationJson);
        Assert.Equal(60, (int)CoapContentFormat.ApplicationCbor);
        Assert.Equal(110, (int)CoapContentFormat.ApplicationSenmlJson);
        Assert.Equal(112, (int)CoapContentFormat.ApplicationSenmlCbor);
        Assert.Equal(11542, (int)CoapContentFormat.ApplicationLwm2mTlv);
        Assert.Equal(11543, (int)CoapContentFormat.ApplicationLwm2mJson);
    }
}
