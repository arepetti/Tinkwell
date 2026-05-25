using System.Text.Json;
using Grpc.Core;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Integration.Tests;

[Collection("Store")]
[Trait("Category", "Integration")]
public class StoreIntegrationTests
{
    private readonly StoreFixture _fixture;
    private readonly StateStore.StateStoreClient _client;

    public StoreIntegrationTests(StoreFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    // -----------------------------------------------------------------------
    // Basic CRUD
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Set_And_Get_RoundTrips()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "sensor.temp", Value = """{"celsius":21.5}"""
        });

        var response = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, Key = "sensor.temp"
        });

        Assert.Equal("""{"celsius":21.5}""", response.Value);
        Assert.NotNull(response.CreatedAt);
        Assert.NotNull(response.UpdatedAt);
    }

    [Fact]
    public async Task Set_Update_PreservesCreatedAt()
    {
        var bucket = UniqueBucket();

        var first = await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """1"""
        });

        await Task.Delay(50);

        var second = await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """2"""
        });

        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.True(second.UpdatedAt >= first.UpdatedAt);
    }

    [Fact]
    public async Task Get_NonExistent_ThrowsNotFound()
    {
        var bucket = UniqueBucket();

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.GetAsync(new GetRequest { BucketId = bucket, Key = "nope" }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingKey_ReturnsFound()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "deleteme", Value = """1"""
        });

        var response = await _client.DeleteAsync(new DeleteRequest
        {
            BucketId = bucket, Key = "deleteme"
        });

        Assert.True(response.Found);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        var bucket = UniqueBucket();

        var response = await _client.DeleteAsync(new DeleteRequest
        {
            BucketId = bucket, Key = "nope"
        });

        Assert.False(response.Found);
    }

    [Fact]
    public async Task Delete_Then_Get_ThrowsNotFound()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """1"""
        });
        await _client.DeleteAsync(new DeleteRequest
        {
            BucketId = bucket, Key = "k"
        });

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.GetAsync(new GetRequest { BucketId = bucket, Key = "k" }).ResponseAsync);
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Namespaces
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Namespaces_IsolateKeys()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, KeyNamespace = "ns1", Key = "k", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, KeyNamespace = "ns2", Key = "k", Value = """2"""
        });

        var r1 = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, KeyNamespace = "ns1", Key = "k"
        });
        var r2 = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, KeyNamespace = "ns2", Key = "k"
        });

        Assert.Equal("""1""", r1.Value);
        Assert.Equal("""2""", r2.Value);
    }

    // -----------------------------------------------------------------------
    // JSON Validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Set_InvalidJson_ThrowsInvalidArgument()
    {
        var bucket = UniqueBucket();

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.SetAsync(new SetRequest
            {
                BucketId = bucket, Key = "k", Value = "not json!"
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.Contains("JSON", ex.Status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Set_MissingBucketId_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.SetAsync(new SetRequest
            {
                Key = "k", Value = """1"""
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Set_MissingKey_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.SetAsync(new SetRequest
            {
                BucketId = "b", Value = """1"""
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Get_MissingBucketId_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.GetAsync(new GetRequest { Key = "k" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // -----------------------------------------------------------------------
    // TTL / Expiration
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Set_WithTtl_ExpiresAfterTimeout()
    {
        var bucket = UniqueBucket();

        var setResponse = await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "ttl-key", Value = """{"temp":true}""",
            TtlSeconds = 2
        });

        Assert.NotNull(setResponse.ExpiresAt);

        // Immediately readable
        var getResponse = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, Key = "ttl-key"
        });
        Assert.Equal("""{"temp":true}""", getResponse.Value);

        // Wait for expiration (TTL + sweep interval buffer)
        await Task.Delay(TimeSpan.FromSeconds(4));

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _client.GetAsync(new GetRequest
            {
                BucketId = bucket, Key = "ttl-key"
            }).ResponseAsync);
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Set_WithTtl_NotVisibleInListAfterExpiry()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "ephemeral", Value = """1""",
            TtlSeconds = 2
        });

        await Task.Delay(TimeSpan.FromSeconds(4));

        var entries = await ListEntriesAsync(bucket);
        Assert.DoesNotContain(entries, e => e.Key == "ephemeral");
    }

    // -----------------------------------------------------------------------
    // List with filters
    // -----------------------------------------------------------------------

    [Fact]
    public async Task List_FilterByBucket()
    {
        var bucket1 = UniqueBucket();
        var bucket2 = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket1, Key = "a", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket2, Key = "b", Value = """2"""
        });

        var result = await ListEntriesAsync(bucket1);
        Assert.Single(result);
        Assert.Equal("a", result[0].Key);
    }

    [Fact]
    public async Task List_FilterByNamespace()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, KeyNamespace = "ns1", Key = "a", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, KeyNamespace = "ns2", Key = "b", Value = """2"""
        });

        var result = await ListEntriesAsync(bucket, keyNamespace: "ns1");
        Assert.Single(result);
        Assert.Equal("a", result[0].Key);
    }

    [Fact]
    public async Task List_FilterByPrefix()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "sensor.temp", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "sensor.hum", Value = """2"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "actuator.fan", Value = """3"""
        });

        var result = await ListEntriesAsync(bucket, prefix: "sensor.");
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.StartsWith("sensor.", e.Key));
    }

    [Fact]
    public async Task List_EmptyBucket_ReturnsEmpty()
    {
        var bucket = UniqueBucket();
        var result = await ListEntriesAsync(bucket);
        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
    // Bucket visibility / ConfigureBucket
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConfigureBucket_Hidden_ExcludedFromCrossBucketList()
    {
        var visibleBucket = UniqueBucket();
        var hiddenBucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = visibleBucket, Key = "v", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = hiddenBucket, Key = "h", Value = """2"""
        });
        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = hiddenBucket, Discoverable = false
        });

        // Cross-bucket list without include_hidden should exclude hidden
        var stream = _client.List(new ListRequest { IncludeHidden = false });
        var entries = await ReadStreamAsync(stream);
        Assert.DoesNotContain(entries, e => e.BucketId == hiddenBucket);
        Assert.Contains(entries, e => e.BucketId == visibleBucket);
    }

    [Fact]
    public async Task ConfigureBucket_Hidden_VisibleWithIncludeHidden()
    {
        var hiddenBucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = hiddenBucket, Key = "h", Value = """1"""
        });
        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = hiddenBucket, Discoverable = false
        });

        // include_hidden = true should include hidden entries
        var stream = _client.List(new ListRequest { IncludeHidden = true });
        var entries = await ReadStreamAsync(stream);
        Assert.Contains(entries, e => e.BucketId == hiddenBucket);
    }

    [Fact]
    public async Task ConfigureBucket_Hidden_StillVisibleWithExplicitBucketId()
    {
        var hiddenBucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = hiddenBucket, Key = "h", Value = """1"""
        });
        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = hiddenBucket, Discoverable = false
        });

        // Explicit bucket_id always works
        var entries = await ListEntriesAsync(hiddenBucket);
        Assert.Single(entries);
    }

    [Fact]
    public async Task ConfigureBucket_ToggleDiscoverable()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """1"""
        });

        // Hide
        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = bucket, Discoverable = false
        });

        var hidden = _client.List(new ListRequest { IncludeHidden = false });
        var hiddenEntries = await ReadStreamAsync(hidden);
        Assert.DoesNotContain(hiddenEntries, e => e.BucketId == bucket);

        // Unhide
        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = bucket, Discoverable = true
        });

        var visible = _client.List(new ListRequest { IncludeHidden = false });
        var visibleEntries = await ReadStreamAsync(visible);
        Assert.Contains(visibleEntries, e => e.BucketId == bucket);
    }

    [Fact]
    public async Task ConfigureBucket_OmittedDiscoverable_DefaultsToDiscoverable()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """1"""
        });

        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = bucket
        });

        var list = _client.List(new ListRequest { IncludeHidden = false });
        var entries = await ReadStreamAsync(list);
        Assert.Contains(entries, e => e.BucketId == bucket);
    }

    // -----------------------------------------------------------------------
    // Watch / Streaming
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Watch_ReceivesSetEvents()
    {
        var bucket = UniqueBucket();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = _client.Watch(new WatchRequest
        {
            BucketId = bucket, IncludeHidden = true
        }, cancellationToken: cts.Token);

        // Give the watcher time to register
        await Task.Delay(500);

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "watched-key", Value = """{"event":"created"}"""
        });

        var events = new List<WatchEvent>();
        await foreach (var e in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count >= 1)
                break;
        }

        Assert.Single(events);
        Assert.Equal(EventType.Set, events[0].EventType);
        Assert.Equal("watched-key", events[0].Key);
        Assert.Equal("""{"event":"created"}""", events[0].Value);
    }

    [Fact]
    public async Task Watch_ReceivesDeleteEvents()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "to-delete", Value = """1"""
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = _client.Watch(new WatchRequest
        {
            BucketId = bucket, IncludeHidden = true
        }, cancellationToken: cts.Token);

        await Task.Delay(500);

        await _client.DeleteAsync(new DeleteRequest
        {
            BucketId = bucket, Key = "to-delete"
        });

        var events = new List<WatchEvent>();
        await foreach (var e in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count >= 1)
                break;
        }

        Assert.Single(events);
        Assert.Equal(EventType.Delete, events[0].EventType);
        Assert.Equal("to-delete", events[0].Key);
    }

    [Fact]
    public async Task Watch_FiltersApply()
    {
        var bucket = UniqueBucket();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = _client.Watch(new WatchRequest
        {
            BucketId = bucket, Prefix = "sensor.", IncludeHidden = true
        }, cancellationToken: cts.Token);

        await Task.Delay(500);

        // This should be filtered out
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "actuator.fan", Value = """1"""
        });

        // This should match
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "sensor.temp", Value = """2"""
        });

        var events = new List<WatchEvent>();
        await foreach (var e in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count >= 1)
                break;
        }

        Assert.Single(events);
        Assert.Equal("sensor.temp", events[0].Key);
    }

    [Fact]
    public async Task Watch_HiddenBucket_ExcludedByDefault()
    {
        var visibleBucket = UniqueBucket();
        var hiddenBucket = UniqueBucket();

        await _client.ConfigureBucketAsync(new ConfigureBucketRequest
        {
            BucketId = hiddenBucket, Discoverable = false
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = _client.Watch(new WatchRequest
        {
            IncludeHidden = false
        }, cancellationToken: cts.Token);

        await Task.Delay(500);

        // Hidden bucket - should be filtered
        await _client.SetAsync(new SetRequest
        {
            BucketId = hiddenBucket, Key = "secret", Value = """1"""
        });

        // Visible bucket - should come through
        await _client.SetAsync(new SetRequest
        {
            BucketId = visibleBucket, Key = "public", Value = """2"""
        });

        var events = new List<WatchEvent>();
        await foreach (var e in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count >= 1)
                break;
        }

        Assert.Single(events);
        Assert.Equal(visibleBucket, events[0].BucketId);
        Assert.Equal("public", events[0].Key);
    }

    [Fact]
    public async Task Watch_MultipleEventsInOrder()
    {
        var bucket = UniqueBucket();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = _client.Watch(new WatchRequest
        {
            BucketId = bucket, IncludeHidden = true
        }, cancellationToken: cts.Token);

        await Task.Delay(500);

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "a", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "b", Value = """2"""
        });
        await _client.DeleteAsync(new DeleteRequest
        {
            BucketId = bucket, Key = "a"
        });

        var events = new List<WatchEvent>();
        await foreach (var e in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count >= 3)
                break;
        }

        Assert.Equal(3, events.Count);
        Assert.Equal(EventType.Set, events[0].EventType);
        Assert.Equal("a", events[0].Key);
        Assert.Equal(EventType.Set, events[1].EventType);
        Assert.Equal("b", events[1].Key);
        Assert.Equal(EventType.Delete, events[2].EventType);
        Assert.Equal("a", events[2].Key);
    }

    // -----------------------------------------------------------------------
    // Multiple values / complex scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleBuckets_Isolated()
    {
        var bucket1 = UniqueBucket();
        var bucket2 = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket1, Key = "k", Value = """1"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket2, Key = "k", Value = """2"""
        });

        var r1 = await _client.GetAsync(new GetRequest { BucketId = bucket1, Key = "k" });
        var r2 = await _client.GetAsync(new GetRequest { BucketId = bucket2, Key = "k" });

        Assert.Equal("""1""", r1.Value);
        Assert.Equal("""2""", r2.Value);
    }

    [Fact]
    public async Task Set_ComplexJson_Preserved()
    {
        var bucket = UniqueBucket();
        var complexJson = """{"devices":[{"id":"d1","sensors":{"temp":21.5,"humidity":45.2}}],"nested":{"a":{"b":{"c":true}}}}""";

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "complex", Value = complexJson
        });

        var response = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, Key = "complex"
        });

        using var expected = JsonDocument.Parse(complexJson);
        using var actual = JsonDocument.Parse(response.Value);
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(actual.RootElement));
    }

    [Fact]
    public async Task Set_OverwriteWithDifferentValue()
    {
        var bucket = UniqueBucket();

        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """{"version":1}"""
        });
        await _client.SetAsync(new SetRequest
        {
            BucketId = bucket, Key = "k", Value = """{"version":2}"""
        });

        var response = await _client.GetAsync(new GetRequest
        {
            BucketId = bucket, Key = "k"
        });
        Assert.Equal("""{"version":2}""", response.Value);
    }

    // -----------------------------------------------------------------------
    // Coordinator pipe commands
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ServiceDiscovery_StoreRegistered()
    {
        var response = await _fixture.Coordinator.SendCommandAsync("service find store");
        var status = response.GetProperty("status").GetString();
        Assert.Equal("ok", status);

        var data = response.GetProperty("data");
        Assert.Equal("store", data.GetProperty("familyName").GetString());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task RunnersListShowsGrpcRunner()
    {
        var response = await _fixture.Coordinator.SendCommandAsync("runners list");
        var status = response.GetProperty("status").GetString();
        Assert.Equal("ok", status);

        var data = response.GetProperty("data");
        Assert.True(data.GetArrayLength() >= 1);

        var runner = data[0];
        Assert.Equal("grpc-store", runner.GetProperty("name").GetString());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<List<Tinkwell.Runlet.Store.Grpc.V1.StoreEntry>> ListEntriesAsync(
        string? bucketId = null, string? keyNamespace = null, string? prefix = null,
        bool includeHidden = false)
    {
        var request = new ListRequest
        {
            BucketId = bucketId ?? "",
            KeyNamespace = keyNamespace ?? "",
            Prefix = prefix ?? "",
            IncludeHidden = includeHidden
        };

        var stream = _client.List(request);
        return await ReadStreamAsync(stream);
    }

    private static async Task<List<Tinkwell.Runlet.Store.Grpc.V1.StoreEntry>> ReadStreamAsync(
        AsyncServerStreamingCall<Tinkwell.Runlet.Store.Grpc.V1.StoreEntry> call)
    {
        var entries = new List<Tinkwell.Runlet.Store.Grpc.V1.StoreEntry>();
        await foreach (var entry in call.ResponseStream.ReadAllAsync())
            entries.Add(entry);
        return entries;
    }

    private static string UniqueBucket() => $"test-{Guid.NewGuid():N}"[..16];
}
