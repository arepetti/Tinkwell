using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Coap.Server.Tests;

public class CoapServerOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = CoapServerOptions.Default;

        Assert.Equal(5683, options.Port);
        Assert.Null(options.Name);
        Assert.Equal(100, options.MaxConcurrentRequests);
        Assert.Equal(200, options.MaxPendingRequests);
        Assert.Equal(CoapBlockSize.Bytes1024, options.ResponseBlockSize);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Block2CacheTtl);
        Assert.Equal(64 * 1024, options.Block1MaxPayloadBytes);
        Assert.Equal(TimeSpan.FromSeconds(247), options.Block1UploadTimeout);
        Assert.Equal(256, options.MaxBlock1Uploads);
        Assert.Equal(256, options.MaxBlock2CacheEntries);
    }

    [Fact]
    public void Default_IsShared()
    {
        Assert.Same(CoapServerOptions.Default, CoapServerOptions.Default);
    }

    [Fact]
    public void Port_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Port = -1 });
    }

    [Fact]
    public void Port_TooLarge_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Port = 70000 });
    }

    [Fact]
    public void Port_Zero_Allowed()
    {
        var options = new CoapServerOptions { Port = 0 };
        Assert.Equal(0, options.Port);
    }

    [Fact]
    public void Port_MaxValid_Allowed()
    {
        var options = new CoapServerOptions { Port = 65535 };
        Assert.Equal(65535, options.Port);
    }

    [Fact]
    public void MaxConcurrentRequests_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxConcurrentRequests = 0 });
    }

    [Fact]
    public void MaxConcurrentRequests_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxConcurrentRequests = -5 });
    }

    [Fact]
    public void MaxConcurrentRequests_One_Allowed()
    {
        var options = new CoapServerOptions { MaxConcurrentRequests = 1 };
        Assert.Equal(1, options.MaxConcurrentRequests);
    }

    [Fact]
    public void MaxPendingRequests_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxPendingRequests = -1 });
    }

    [Fact]
    public void MaxPendingRequests_Zero_Allowed()
    {
        var options = new CoapServerOptions { MaxPendingRequests = 0 };
        Assert.Equal(0, options.MaxPendingRequests);
    }

    [Fact]
    public void ObjectInitializer_AllFields()
    {
        var options = new CoapServerOptions
        {
            Port = 1234,
            Name = "test",
            MaxConcurrentRequests = 5,
            MaxPendingRequests = 10,
            ResponseBlockSize = CoapBlockSize.Bytes256,
            Block2CacheTtl = TimeSpan.FromSeconds(5),
            Block1MaxPayloadBytes = 8192,
            Block1UploadTimeout = TimeSpan.FromSeconds(30),
            MaxBlock1Uploads = 10,
            MaxBlock2CacheEntries = 20,
        };

        Assert.Equal(1234, options.Port);
        Assert.Equal("test", options.Name);
        Assert.Equal(5, options.MaxConcurrentRequests);
        Assert.Equal(10, options.MaxPendingRequests);
        Assert.Equal(CoapBlockSize.Bytes256, options.ResponseBlockSize);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Block2CacheTtl);
        Assert.Equal(8192, options.Block1MaxPayloadBytes);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Block1UploadTimeout);
        Assert.Equal(10, options.MaxBlock1Uploads);
        Assert.Equal(20, options.MaxBlock2CacheEntries);
    }

    [Fact]
    public void MaxBlock1Uploads_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxBlock1Uploads = -1 });
    }

    [Fact]
    public void MaxBlock1Uploads_Zero_Allowed()
    {
        var options = new CoapServerOptions { MaxBlock1Uploads = 0 };
        Assert.Equal(0, options.MaxBlock1Uploads);
    }

    [Fact]
    public void MaxBlock2CacheEntries_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxBlock2CacheEntries = -1 });
    }

    [Fact]
    public void MaxBlock2CacheEntries_Zero_Allowed()
    {
        var options = new CoapServerOptions { MaxBlock2CacheEntries = 0 };
        Assert.Equal(0, options.MaxBlock2CacheEntries);
    }

    [Fact]
    public void ResponseBlockSize_Null_DisablesSplitting()
    {
        var options = new CoapServerOptions { ResponseBlockSize = null };
        Assert.Null(options.ResponseBlockSize);
    }

    [Fact]
    public void Block1MaxPayloadBytes_Zero_Allowed()
    {
        var options = new CoapServerOptions { Block1MaxPayloadBytes = 0 };
        Assert.Equal(0, options.Block1MaxPayloadBytes);
    }

    [Fact]
    public void Block1MaxPayloadBytes_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Block1MaxPayloadBytes = -1 });
    }

    [Fact]
    public void Block2CacheTtl_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Block2CacheTtl = TimeSpan.Zero });
    }

    [Fact]
    public void Block2CacheTtl_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Block2CacheTtl = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void Block1UploadTimeout_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Block1UploadTimeout = TimeSpan.Zero });
    }

    [Fact]
    public void Block1UploadTimeout_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { Block1UploadTimeout = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void Default_DedupValues()
    {
        var options = CoapServerOptions.Default;
        Assert.Equal(TimeSpan.FromSeconds(247), options.DedupTtl);
        Assert.Equal(1024, options.MaxDedupEntries);
    }

    [Fact]
    public void DedupTtl_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { DedupTtl = TimeSpan.Zero });
    }

    [Fact]
    public void DedupTtl_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { DedupTtl = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void MaxDedupEntries_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoapServerOptions { MaxDedupEntries = -1 });
    }

    [Fact]
    public void MaxDedupEntries_Zero_Allowed()
    {
        // Documented escape hatch: 0 disables deduplication entirely. The validator must allow
        // it (the disablement is implemented by treating 0 as "no entries kept", not by
        // rejecting the configuration).
        var options = new CoapServerOptions { MaxDedupEntries = 0 };
        Assert.Equal(0, options.MaxDedupEntries);
    }

    [Fact]
    public void ObserveRegistrationPredicate_DefaultIsNull()
    {
        Assert.Null(CoapServerOptions.Default.ObserveRegistrationPredicate);
    }

    [Fact]
    public void ObserveRegistrationPredicate_CanBeAssigned()
    {
        Func<byte, bool> p = code => code == CoapCode.Content;
        var options = new CoapServerOptions { ObserveRegistrationPredicate = p };
        Assert.Same(p, options.ObserveRegistrationPredicate);
    }

    [Fact]
    public void ParseLimits_DefaultIsRecommendedInstance()
    {
        var d = CoapServerOptions.Default.ParseLimits;
        Assert.Equal(CoapMessageParseLimits.Default.MaxMessageSize, d.MaxMessageSize);
        Assert.Equal(CoapMessageParseLimits.Default.MaxOptionCount, d.MaxOptionCount);
        Assert.Equal(CoapMessageParseLimits.Default.MaxOptionValueLength, d.MaxOptionValueLength);
    }

    [Fact]
    public void ParseLimits_CanBeOverridden()
    {
        var custom = new CoapMessageParseLimits(1024, 8, 256);
        var options = new CoapServerOptions { ParseLimits = custom };
        Assert.Equal(1024, options.ParseLimits.MaxMessageSize);
        Assert.Equal(8, options.ParseLimits.MaxOptionCount);
        Assert.Equal(256, options.ParseLimits.MaxOptionValueLength);
    }
}
