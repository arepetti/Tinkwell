using Tinkwell.Coap;

namespace Tinkwell.Coap.Tests;

/// <summary>
/// Pins the contract of the <c>extraOptions</c> seam on
/// <see cref="CoapMessage.BuildRequest(CoapMessageType, byte, ushort, byte[], string, string?, CoapContentFormat?, CoapContentFormat?, byte[]?, CoapBlockOption?, CoapBlockOption?, int?, IEnumerable{CoapOption}?)"/>
/// and <see cref="CoapMessage.BuildResponse(CoapMessageType, byte, ushort, byte[], CoapContentFormat?, byte[]?, int?, CoapBlockOption?, CoapBlockOption?, int?, IEnumerable{CoapOption}?)"/>:
/// extras are written in ascending option-number order, the original input order is preserved
/// among entries with the same number (stable sort), and bad inputs are rejected.
/// </summary>
public class CoapMessageBuildExtraOptionsTests
{
    /// <summary>RFC 7252, Section 5.10.6 - ETag option number.</summary>
    private const int ETagOptionNumber = 4;
    /// <summary>RFC 7252, Section 5.10.5 - Max-Age option number.</summary>
    private const int MaxAgeOptionNumber = 14;
    /// <summary>RFC 7252, Section 5.10.8.1 - If-Match option number.</summary>
    private const int IfMatchOptionNumber = 1;

    [Fact]
    public void BuildRequest_NullExtras_ProducesSameWireOutputAsBuilderWithoutExtras()
    {
        var withoutExtras = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get, 1, [], "/foo");

        var withNull = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get, 1, [], "/foo",
            extraOptions: null);

        Assert.Equal(withoutExtras, withNull);
    }

    [Fact]
    public void BuildRequest_ExtrasInterleavedByNumber_RoundTripsAscending()
    {
        // ETag (4) is below Uri-Path (11), Max-Age (14) sits between Uri-Path (11) and Uri-Query
        // (15). The serialiser must emit them in numeric order regardless of the order we passed
        // them in; once parsed, the option list comes back monotonically non-decreasing.
        byte[] etag = [0xAA, 0xBB];
        byte[] maxAgeValue = [60];

        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/sensors/temp",
            query: "unit=c",
            extraOptions: new[]
            {
                new CoapOption(MaxAgeOptionNumber, maxAgeValue),
                new CoapOption(ETagOptionNumber, etag),
            });

        var parsed = CoapMessage.Parse(datagram);

        var numbers = parsed.Options.Select(o => o.Number).ToArray();
        for (int i=1; i < numbers.Length; ++i)
            Assert.True(numbers[i] >= numbers[i - 1],
                $"Option numbers must be monotonically non-decreasing on the wire (saw {numbers[i - 1]} then {numbers[i]}).");

        // Original semantics: the ETag and Max-Age values made it through.
        Assert.Equal(etag, parsed.Options.Single(o => o.Number == ETagOptionNumber).Value);
        Assert.Equal(maxAgeValue, parsed.Options.Single(o => o.Number == MaxAgeOptionNumber).Value);

        // Builder-supplied options coexist with the extras.
        Assert.Equal(2, parsed.Options.Count(o => o.Number == CoapOptionNumber.UriPath));
        Assert.Single(parsed.Options, o => o.Number == CoapOptionNumber.UriQuery);
    }

    [Fact]
    public void BuildRequest_DuplicateOptionNumbers_PreserveInputOrder()
    {
        // Two ETags supplied in a specific order. CoAP allows multiple ETags (RFC 7252,
        // Section 5.10.6) and the serialiser is documented to keep their relative order so
        // proxies and parsers see the same sequence the application expressed.
        byte[] first = [0x01];
        byte[] second = [0x02];
        byte[] third = [0x03];

        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/r",
            extraOptions: new[]
            {
                new CoapOption(ETagOptionNumber, first),
                new CoapOption(ETagOptionNumber, second),
                new CoapOption(ETagOptionNumber, third),
            });

        var parsed = CoapMessage.Parse(datagram);
        var etags = parsed.Options
            .Where(o => o.Number == ETagOptionNumber)
            .Select(o => o.Value)
            .ToArray();

        Assert.Equal(3, etags.Length);
        Assert.Equal(first, etags[0]);
        Assert.Equal(second, etags[1]);
        Assert.Equal(third, etags[2]);
    }

    [Fact]
    public void BuildRequest_ExtrasAndBuilderArgsAtSameOptionNumber_BuilderArgFirst()
    {
        // The builder pushes its convenience options into the list before extras
        // (CoapMessage.BuildRequest body), so when the user adds an extra Uri-Path the
        // path-derived segments must precede the extra. Insertion sort is stable, so the
        // relative order is preserved even though both entries carry the same option number.
        var datagram = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get,
            messageId: 1, token: [],
            path: "/a/b",
            extraOptions: new[]
            {
                new CoapOption(CoapOptionNumber.UriPath, "extra"u8.ToArray()),
            });

        var parsed = CoapMessage.Parse(datagram);
        var paths = parsed.Options
            .Where(o => o.Number == CoapOptionNumber.UriPath)
            .Select(o => o.AsString())
            .ToArray();

        Assert.Equal(["a", "b", "extra"], paths);
    }

    [Fact]
    public void BuildRequest_ExtraOptionWithNullValue_Throws()
    {
        var bad = new[] { new CoapOption(IfMatchOptionNumber, null!) };

        Assert.Throws<ArgumentNullException>(() =>
            CoapMessage.BuildRequest(
                CoapMessageType.Confirmable, CoapCode.Get,
                messageId: 1, token: [],
                path: "/x",
                extraOptions: bad));
    }

    [Fact]
    public void BuildRequest_ExtraOptionWithNegativeNumber_Throws()
    {
        var bad = new[] { new CoapOption(-1, [0]) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoapMessage.BuildRequest(
                CoapMessageType.Confirmable, CoapCode.Get,
                messageId: 1, token: [],
                path: "/x",
                extraOptions: bad));
    }

    [Fact]
    public void BuildResponse_ExtrasInterleavedByNumber_RoundTripsAscending()
    {
        byte[] locationSegment = "created"u8.ToArray();
        byte[] etag = [0x42];

        var datagram = CoapMessage.BuildResponse(
            CoapMessageType.Acknowledgement,
            CoapCode.Created,
            messageId: 1, token: [],
            contentFormat: CoapContentFormat.TextPlain,
            payload: "ok"u8.ToArray(),
            extraOptions: new[]
            {
                new CoapOption(CoapOptionNumber.LocationPath, locationSegment),
                new CoapOption(ETagOptionNumber, etag),
            });

        var parsed = CoapMessage.Parse(datagram);

        var numbers = parsed.Options.Select(o => o.Number).ToArray();
        for (int i=1; i < numbers.Length; ++i)
            Assert.True(numbers[i] >= numbers[i - 1]);

        Assert.Equal(etag, parsed.Options.Single(o => o.Number == ETagOptionNumber).Value);
        Assert.Equal("/created", parsed.LocationPath);
        Assert.Equal(CoapContentFormat.TextPlain, parsed.RequestContentFormat);
    }

    [Fact]
    public void BuildResponse_ExtraOptionWithNullValue_Throws()
    {
        var bad = new[] { new CoapOption(ETagOptionNumber, null!) };

        Assert.Throws<ArgumentNullException>(() =>
            CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement, CoapCode.Content,
                messageId: 1, token: [],
                contentFormat: null, payload: null,
                extraOptions: bad));
    }

    [Fact]
    public void BuildResponse_ExtraOptionWithNegativeNumber_Throws()
    {
        var bad = new[] { new CoapOption(-3, [0]) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoapMessage.BuildResponse(
                CoapMessageType.Acknowledgement, CoapCode.Content,
                messageId: 1, token: [],
                contentFormat: null, payload: null,
                extraOptions: bad));
    }

    [Fact]
    public void BuildRequest_EmptyExtras_NoEffect()
    {
        var withoutExtras = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get, 1, [], "/foo");

        var withEmpty = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable, CoapCode.Get, 1, [], "/foo",
            extraOptions: Array.Empty<CoapOption>());

        Assert.Equal(withoutExtras, withEmpty);
    }
}
