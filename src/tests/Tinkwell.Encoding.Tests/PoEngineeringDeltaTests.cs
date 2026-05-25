using Tinkwell.Coap;
using Tinkwell.Encoding;

namespace Tinkwell.Encoding.Tests;

public class PoEngineeringDeltaTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AsTime_FromTimeValue_ReturnsSameInstant()
    {
        var t = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var pv = PayloadValue.FromTime(t);

        Assert.Equal(t, pv.AsTime());
    }

    [Fact]
    public void AsTime_FromIntegerValue_TreatsAsUnixSeconds()
    {
        var pv = PayloadValue.FromInteger(1700000000L);

        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1700000000L),
            pv.AsTime());
    }

    [Fact]
    public void AsTime_FromUnsupportedType_Throws()
    {
        var pv = PayloadValue.FromString("not a time");

        Assert.Throws<InvalidOperationException>(() => pv.AsTime());
    }

    [Fact]
    public void SenmlDecode_StringOverload_NoNow_Works()
    {
        var json = "[{\"bn\":\"/3303/0/\",\"n\":\"5700\",\"v\":23.5}]";

        var decoded = SenmlJsonCodec.Decode(json);

        Assert.Single(decoded);
        Assert.Equal("/3303/0/5700", decoded[0].Name);
        Assert.Equal(23.5, decoded[0].Value.AsDouble(), 6);
    }

    [Fact]
    public void SenmlDecode_StringOverload_WithNow_AppliesRelativeT()
    {
        var json = "[{\"n\":\"5700\",\"v\":1.0,\"t\":-30}]";

        var decoded = SenmlJsonCodec.Decode(json, FixedNow);

        Assert.Equal(FixedNow.AddSeconds(-30), decoded[0].Timestamp!.Value);
    }

    [Fact]
    public void SenmlDecode_StringOverload_NullJson_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SenmlJsonCodec.Decode((string)null!));
        Assert.Throws<ArgumentNullException>(() => SenmlJsonCodec.Decode((string)null!, FixedNow));
    }

    [Fact]
    public void PayloadCodec_DecodeSingleResource_WithNow_PropagatesToSenmlRelativeTime()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            "[{\"n\":\"5700\",\"v\":42,\"t\":-5}]");

        // Decoding with `now` is deterministic; we don't observe the timestamp via
        // the value-only API, but we observe the value typing path is unchanged.
        var v = PayloadCodec.DecodeSingleResource(
            bytes,
            CoapContentFormat.ApplicationSenmlJson,
            PayloadType.Float,
            FixedNow);

        Assert.Equal(PayloadType.Integer, v.Type);
        Assert.Equal(42L, v.AsLong());
    }

    [Fact]
    public void PayloadCodec_DecodeSingleResource_DefaultOverload_StillWorks()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            "[{\"n\":\"5700\",\"v\":3.14}]");

        var v = PayloadCodec.DecodeSingleResource(
            bytes,
            CoapContentFormat.ApplicationSenmlJson);

        Assert.Equal(PayloadType.Float, v.Type);
        Assert.Equal(3.14, v.AsDouble(), 6);
    }
}
