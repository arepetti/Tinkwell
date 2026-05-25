using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

public class CoapClientRequestOptionsTests
{
    [Fact]
    public void Timeout_Zero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { Timeout = TimeSpan.Zero });
    }

    [Fact]
    public void Timeout_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { Timeout = TimeSpan.FromMilliseconds(-1) });
    }

    [Fact]
    public void TotalTimeout_Zero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { TotalTimeout = TimeSpan.Zero });
    }

    [Fact]
    public void TotalTimeout_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { TotalTimeout = TimeSpan.FromTicks(-1) });
    }

    [Fact]
    public void AckTimeout_Zero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { AckTimeout = TimeSpan.Zero });
    }

    [Fact]
    public void AckTimeout_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { AckTimeout = TimeSpan.FromMilliseconds(-1) });
    }

    [Fact]
    public void AckRandomFactor_BelowOne_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { AckRandomFactor = 0.99 });
    }

    [Fact]
    public void AckRandomFactor_One_IsAllowed()
    {
        _ = new CoapClientRequestOptions { AckRandomFactor = 1.0 };
    }

    [Fact]
    public void MaxRetransmit_Negative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CoapClientRequestOptions { MaxRetransmit = -1 });
    }

    [Fact]
    public void MaxRetransmit_Zero_IsAllowed()
    {
        var o = new CoapClientRequestOptions { MaxRetransmit = 0 };
        Assert.Equal(0, o.MaxRetransmit);
    }

    [Fact]
    public void Default_IsUsableAndImmutablePattern()
    {
        Assert.NotNull(CoapClientRequestOptions.Default);
        Assert.Equal(TimeSpan.FromSeconds(5), CoapClientRequestOptions.Default.Timeout);
    }
}
