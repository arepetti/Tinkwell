using Tinkwell.Events;
using Tinkwell.Runlet.Actions;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Runlet.Actions.Tests;

public class EventBusRequestFactoryTests
{
    [Fact]
    public void ToPublishRequest_MapsSourceNameVerbObjectCorrelationAndPayload()
    {
        var ts = new DateTime(2024, 3, 2, 15, 30, 0, DateTimeKind.Utc);
        var envelope = new EventEnvelope
        {
            Source = "device-a",
            Verb = EventVerb.Changed,
            Name = "pressure",
            Object = "1013.2",
            CorrelationId = "cid-1",
            Timestamp = ts,
            Payload = new Dictionary<string, string> { ["unit"] = "hPa" },
        };

        var request = EventBusRequestFactory.ToPublishRequest(envelope);

        Assert.Equal("device-a", request.Source);
        Assert.Equal(EventsGrpc.EventVerb.Changed, request.Verb);
        Assert.Equal("pressure", request.Name);
        Assert.Equal("1013.2", request.Object);
        Assert.Equal("cid-1", request.CorrelationId);
        Assert.Equal("hPa", request.Payload["unit"]);
    }

    [Fact]
    public void ToPublishRequest_TimestampIsUtcInProto()
    {
        var local = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var envelope = new EventEnvelope
        {
            Source = "s",
            Verb = EventVerb.Fired,
            Name = "e",
            Timestamp = local,
        };

        var request = EventBusRequestFactory.ToPublishRequest(envelope);
        var roundTrip = request.Timestamp?.ToDateTime() ?? default;
        Assert.Equal(DateTimeKind.Utc, roundTrip.Kind);
    }

    [Fact]
    public void ToPublishRequest_OmitsOptionalFieldsWhenNull()
    {
        var envelope = new EventEnvelope
        {
            Source = "s",
            Verb = EventVerb.Fired,
            Name = "e",
            Object = null,
            CorrelationId = null,
        };

        var request = EventBusRequestFactory.ToPublishRequest(envelope);

        Assert.Equal("", request.Object);
        Assert.Equal("", request.CorrelationId);
    }

    [Fact]
    public void ToPublishRequest_CustomVerb_SetWhenNonStandardVerb()
    {
        var envelope = new EventEnvelope
        {
            Source = "s",
            Verb = EventVerb.Custom,
            CustomVerb = "bespoke",
            Name = "e",
        };

        var request = EventBusRequestFactory.ToPublishRequest(envelope);

        Assert.Equal(EventsGrpc.EventVerb.Custom, request.Verb);
        Assert.Equal("bespoke", request.CustomVerb);
    }
}
