using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Events;
using Tinkwell.Runlet.Events;

namespace Tinkwell.Runlet.Events.Tests;

public class EventFanOutTests
{
    /// <summary>
    /// Uses <see cref="BoundedChannelFullMode.DropOldest"/> so tests focus on delivery
    /// behavior; production defaults to <see cref="BoundedChannelFullMode.DropWrite"/>.
    /// DropOldest avoids the channel drop-tracker path; drop counting and
    /// <c>tinkwell.channel.drops</c> behavior are covered by <c>ChannelDropTracker</c> tests.
    /// </summary>
    private static EventFanOut CreateFanOut(int capacity = 100) =>
        new(new EventFanOutConfig(new ChannelConfig(capacity, BoundedChannelFullMode.DropOldest)),
            NullLogger<EventFanOut>.Instance);

    private static async Task WaitForSubscribersAsync(EventFanOut fanOut, int expected, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (fanOut.SubscriberCount < expected)
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException(
                    $"Expected {expected} subscriber(s) but only {fanOut.SubscriberCount} registered within {timeoutMs}ms");
            }

            await Task.Delay(10);
        }
    }

    private static EventEnvelope MakeEvent(string name, string source = "test",
        EventVerb verb = EventVerb.Changed, string? correlationId = null) => new()
    {
        Source = source,
        Verb = verb,
        Name = name,
        CorrelationId = correlationId,
    };

    [Fact]
    public async Task Publish_DeliveredToSubscriber()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a"));
        fanOut.Publish(MakeEvent("b"));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, received.Count);
        Assert.Equal("a", received[0].Name);
        Assert.Equal("b", received[1].Name);
    }

    [Fact]
    public async Task SubscribeFilter_BySource_FiltersCorrectly()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { Source = "signals" };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", source: "measures"));
        fanOut.Publish(MakeEvent("b", source: "signals"));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("b", received[0].Name);
    }

    [Fact]
    public async Task SubscribeFilter_ByVerb_FiltersCorrectly()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { Verbs = [EventVerb.Fired] };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", verb: EventVerb.Changed));
        fanOut.Publish(MakeEvent("b", verb: EventVerb.Fired));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("b", received[0].Name);
    }

    [Fact]
    public async Task SubscribeFilter_ByNamePrefix_MatchesPrefix()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { NamePrefix = "sensor" };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("sensor.temperature"));
        fanOut.Publish(MakeEvent("sensor.humidity"));
        fanOut.Publish(MakeEvent("actuator.valve"));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => e.Name == "sensor.temperature");
        Assert.Contains(received, e => e.Name == "sensor.humidity");
        Assert.DoesNotContain(received, e => e.Name == "actuator.valve");
    }

    [Fact]
    public async Task SubscribeFilter_ByNamePrefix_CaseInsensitive()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { NamePrefix = "sensor" };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("Sensor.Temperature"));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("Sensor.Temperature", received[0].Name);
    }

    [Fact]
    public async Task CorrelationId_PreservedThroughFanOut()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        EventEnvelope? received = null;
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
            {
                received = e;
                await cts.CancelAsync();
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", correlationId: "abc12345"));

        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.NotNull(received);
        Assert.Equal("abc12345", received.CorrelationId);
    }

    [Fact]
    public async Task MultipleSubscribers_AllReceiveEvents()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();

        var received1 = new List<EventEnvelope>();
        var received2 = new List<EventEnvelope>();

        async Task SubscribeAsync(List<EventEnvelope> sink)
        {
            await foreach (var e in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
            {
                sink.Add(e);
                if (sink.Count >= 1)
                {
                    break;
                }
            }
        }

        var sub1 = Task.Run(() => SubscribeAsync(received1));
        var sub2 = Task.Run(() => SubscribeAsync(received2));

        await WaitForSubscribersAsync(fanOut, 2);
        fanOut.Publish(MakeEvent("broadcast"));

        await Task.WhenAny(Task.WhenAll(sub1, sub2), Task.Delay(2000));
        await cts.CancelAsync();

        Assert.Single(received1);
        Assert.Single(received2);
        Assert.Equal("broadcast", received1[0].Name);
        Assert.Equal("broadcast", received2[0].Name);
    }

    [Fact]
    public void Publish_WithoutSubscribers_DoesNotThrow()
    {
        var fanOut = CreateFanOut();
        var ex = Record.Exception(() => fanOut.Publish(MakeEvent("only")));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("signals")]
    [InlineData("SIGNALS")]
    [InlineData("SiGnAlS")]
    public async Task SubscribeFilter_BySource_IsCaseInsensitive(string filterSource)
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { Source = filterSource };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("x", source: "SignAlS"));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
    }

    [Fact]
    public async Task SubscribeFilter_Empty_VerbsHashSet_MatchesAllVerbs_SameAsNull()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter { Verbs = new HashSet<EventVerb>() };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", verb: EventVerb.Changed));
        fanOut.Publish(MakeEvent("b", verb: EventVerb.Deleted));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => e.Name == "a" && e.Verb == EventVerb.Changed);
        Assert.Contains(received, e => e.Name == "b" && e.Verb == EventVerb.Deleted);
    }

    [Fact]
    public async Task SubscribeFilter_Combined_SourceAndVerb_RequiresBoth()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter
            {
                Source = "signals",
                Verbs = [EventVerb.Fired],
            };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", source: "signals", verb: EventVerb.Changed));
        fanOut.Publish(MakeEvent("b", source: "other", verb: EventVerb.Fired));
        fanOut.Publish(MakeEvent("c", source: "signals", verb: EventVerb.Fired));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("c", received[0].Name);
    }

    [Fact]
    public async Task SubscribeFilter_Combined_SourceAndNamePrefix_RequiresBoth()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter
            {
                Source = "measures",
                NamePrefix = "tank",
            };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("tank.level", source: "signals"));
        fanOut.Publish(MakeEvent("room.temp", source: "measures"));
        fanOut.Publish(MakeEvent("tank.level", source: "measures"));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("tank.level", received[0].Name);
    }

    [Fact]
    public async Task SubscribeFilter_Combined_AllThree_RequiresAll()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            var filter = new SubscribeFilter
            {
                Source = "signals",
                Verbs = [EventVerb.Changed, EventVerb.Created],
                NamePrefix = "io.",
            };
            await foreach (var e in fanOut.SubscribeAsync(filter, cts.Token))
            {
                received.Add(e);
                if (received.Count >= 1)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("io.button", source: "signals", verb: EventVerb.Changed));
        fanOut.Publish(MakeEvent("io.button", source: "signals", verb: EventVerb.Deleted));
        fanOut.Publish(MakeEvent("ui.panel", source: "signals", verb: EventVerb.Changed));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Single(received);
        Assert.Equal("io.button", received[0].Name);
    }

    [Fact]
    public async Task Subscribe_AfterCancel_SubscriberCountReturnsToZero()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var subscriberTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
                {
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        Assert.Equal(1, fanOut.SubscriberCount);
        await cts.CancelAsync();
        await subscriberTask;
        Assert.Equal(0, fanOut.SubscriberCount);
    }

    [Fact]
    public async Task EventEnvelope_ObjectCustomVerbTimestampPayload_PreservedThroughFanOut()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        EventEnvelope? received = null;
        var expectedTs = new DateTime(2018, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        var published = new EventEnvelope
        {
            Source = "test",
            Verb = EventVerb.Custom,
            CustomVerb = "nudge",
            Name = "x.y",
            Object = "ref-42",
            CorrelationId = "corr-1",
            Timestamp = expectedTs,
            Payload = new Dictionary<string, string> { ["unit"] = "C", ["zone"] = "lab-a" },
        };
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
            {
                received = e;
                await cts.CancelAsync();
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(published);
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.NotNull(received);
        Assert.Equal(published.Source, received.Source);
        Assert.Equal(published.Verb, received.Verb);
        Assert.Equal("nudge", received.CustomVerb);
        Assert.Equal(published.Name, received.Name);
        Assert.Equal("ref-42", received.Object);
        Assert.Equal("corr-1", received.CorrelationId);
        Assert.Equal(expectedTs, received.Timestamp);
        Assert.Equal(2, received.Payload.Count);
        Assert.Equal("C", received.Payload["unit"]);
        Assert.Equal("lab-a", received.Payload["zone"]);
    }

    [Fact]
    public async Task SubscribeFilter_Empty_DeliversEventsFromAllSourcesAndVerbs()
    {
        var fanOut = CreateFanOut();
        using var cts = new CancellationTokenSource();
        var received = new List<EventEnvelope>();
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in fanOut.SubscribeAsync(new SubscribeFilter(), cts.Token))
            {
                received.Add(e);
                if (received.Count >= 2)
                {
                    await cts.CancelAsync();
                }
            }
        });

        await WaitForSubscribersAsync(fanOut, 1);
        fanOut.Publish(MakeEvent("a", source: "alpha", verb: EventVerb.Fired));
        fanOut.Publish(MakeEvent("b", source: "beta", verb: EventVerb.Deleted));
        try
        {
            await subscriberTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => e.Name == "a" && e.Source == "alpha" && e.Verb == EventVerb.Fired);
        Assert.Contains(received, e => e.Name == "b" && e.Source == "beta" && e.Verb == EventVerb.Deleted);
    }
}
