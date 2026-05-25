using SysEncoding = System.Text.Encoding;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class SenmlJsonTimeRulesTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const long AbsoluteThreshold = 1L << 28;

    [Fact]
    public void Decode_AbsoluteT_TakenAsIs_NoBaseTimeAdded()
    {
        var json = SysEncoding.UTF8.GetBytes(
            "[{\"bn\":\"/3303/0/\",\"bt\":1000,\"n\":\"5700\",\"v\":1.0,\"t\":2000000000}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Single(decoded);
        Assert.NotNull(decoded[0].Timestamp);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(2000000000),
            decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_RelativeT_AppliedRelativeToNow()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":1.0,\"t\":10}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(FixedNow.AddSeconds(10), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_NegativeRelativeT_AppliedRelativeToNow()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":1.0,\"t\":-10}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(FixedNow.AddSeconds(-10), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_BaseTimePlusRelativeT_BothBelowThreshold_RelativeToNow()
    {
        var json = SysEncoding.UTF8.GetBytes(
            "[{\"bt\":5,\"n\":\"5700\",\"v\":1.0,\"t\":7}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(FixedNow.AddSeconds(12), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_BaseTimePlusT_SumExceedsThreshold_TreatedAsAbsolute()
    {
        long bt = AbsoluteThreshold - 1;
        long t = 5;
        long sum = bt + t;

        var json = SysEncoding.UTF8.GetBytes(
            $"[{{\"bt\":{bt},\"n\":\"5700\",\"v\":1.0,\"t\":{t}}}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(sum), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_BaseTimeOnly_ResolvedWithoutT()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"bt\":12,\"n\":\"5700\",\"v\":1.0}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(FixedNow.AddSeconds(12), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_NoTimeFields_ProducesNoTimestamp()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":1.0}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Null(decoded[0].Timestamp);
    }

    [Fact]
    public void Decode_TExactlyAtThreshold_TreatedAsAbsolute()
    {
        long t = AbsoluteThreshold;
        var json = SysEncoding.UTF8.GetBytes(
            $"[{{\"bt\":1000,\"n\":\"5700\",\"v\":1.0,\"t\":{t}}}]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        // At threshold, absolute branch wins; bt is not added.
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(t), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void Decode_BaseTimeStickyAcrossRecords()
    {
        // bt declared on record 0 must apply to record 1 even though record 1 has no bt of its own.
        var json = SysEncoding.UTF8.GetBytes(
            "[" +
            "{\"bn\":\"/3303/0/\",\"bt\":5,\"n\":\"5700\",\"v\":1.0,\"t\":1}," +
            "{\"n\":\"5701\",\"v\":2.0,\"t\":2}" +
            "]");

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(2, decoded.Count);
        Assert.Equal(FixedNow.AddSeconds(6), decoded[0].Timestamp!.Value); // 5 + 1
        Assert.Equal(FixedNow.AddSeconds(7), decoded[1].Timestamp!.Value); // 5 + 2 (sticky bt)
    }

    [Fact]
    public void Decode_DefaultNow_UsesUtcNow()
    {
        var json = SysEncoding.UTF8.GetBytes("[{\"n\":\"5700\",\"v\":1.0,\"t\":0}]");

        var before = DateTimeOffset.UtcNow;
        var decoded = SenmlJsonCodec.Decode(json);
        var after = DateTimeOffset.UtcNow;

        var ts = decoded[0].Timestamp!.Value;
        Assert.InRange(ts, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
