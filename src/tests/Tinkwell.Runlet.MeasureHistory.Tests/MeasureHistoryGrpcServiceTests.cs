using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Measures.History;
using Tinkwell.Runlet.MeasureHistory.Grpc.V1;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

public sealed class MeasureHistoryGrpcServiceTests
{
    private static (MeasureHistoryGrpcService Service, FakeMeasureHistoryStore Store, MeasureHistoryStoreHolder Holder) CreateSut(
        FakeMeasureHistoryStore? store = null)
    {
        store ??= new FakeMeasureHistoryStore();
        var holder = new MeasureHistoryStoreHolder();
        holder.Set(store);
        var service = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);
        return (service, store, holder);
    }

    [Fact]
    public async Task QueryAsync_delegates_to_store_with_expected_domain_query()
    {
        var (svc, fake, _) = CreateSut();
        var t0 = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 1, 11, 12, 0, 0, DateTimeKind.Utc);
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "a", Timestamp = t0, NumericValue = 1 });
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "a", Timestamp = t1, NumericValue = 2 });

        var req = new QueryRequest
        {
            Name = "a",
            FromUnixMs = new DateTimeOffset(t0).ToUnixTimeMilliseconds(),
            ToUnixMs = new DateTimeOffset(t1.AddSeconds(1)).ToUnixTimeMilliseconds(),
        };

        var resp = await svc.Query(req, new TestServerCallContext());

        Assert.Equal(2, resp.Points.Count);
        Assert.False(resp.HasMore);
        Assert.NotNull(fake.LastQuery);
        Assert.Equal("a", fake.LastQuery!.Name);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(req.FromUnixMs).UtcDateTime,
            fake.LastQuery.From);
    }

    [Fact]
    public async Task Query_response_maps_numeric_string_opaque_and_unit_from_domain_points()
    {
        var (svc, fake, _) = CreateSut();
        var ts = new DateTime(2026, 4, 27, 15, 30, 0, DateTimeKind.Utc);
        await fake.WriteAsync(new MeasureHistoryPoint
        {
            Name = "probe",
            Timestamp = ts,
            NumericValue = 42,
            StringValue = "ok",
            OpaqueValue = [0x10, 0x20],
            Unit = "mV",
        });

        var resp = await svc.Query(new QueryRequest { Name = "probe" }, new TestServerCallContext());

        Assert.Single(resp.Points);
        var p = resp.Points[0];
        Assert.Equal("probe", p.Name);
        Assert.Equal(42, p.NumericValue);
        Assert.Equal("ok", p.StringValue);
        Assert.Equal(new byte[] { 0x10, 0x20 }, p.OpaqueValue.ToByteArray());
        Assert.Equal("mV", p.Unit);
        Assert.Equal(new DateTimeOffset(ts).ToUnixTimeMilliseconds(), p.TimestampUnixMs);
    }

    [Fact]
    public async Task Query_response_maps_null_Unit_to_empty_proto_string()
    {
        var (svc, fake, _) = CreateSut();
        await fake.WriteAsync(new MeasureHistoryPoint
        {
            Name = "nounit",
            Timestamp = DateTime.UtcNow,
            NumericValue = 1,
            Unit = null,
        });

        var resp = await svc.Query(new QueryRequest { Name = "nounit" }, new TestServerCallContext());

        Assert.Single(resp.Points);
        Assert.Equal("", resp.Points[0].Unit);
    }

    [Fact]
    public async Task GetDefinitionsAsync_returns_stored_definitions()
    {
        var (svc, fake, _) = CreateSut();
        var def = new MeasureDefinitionSnapshot
        {
            Name = "m",
            Type = "Number",
            Description = "d",
        };
        await fake.SyncDefinitionAsync(def);

        var resp = await svc.GetDefinitions(new GetDefinitionsRequest(), new TestServerCallContext());

        Assert.Single(resp.Definitions);
        Assert.Equal("m", resp.Definitions[0].Name);
        Assert.Equal("d", resp.Definitions[0].Description);
    }

    [Fact]
    public async Task Fake_store_accumulates_writes_correctly()
    {
        var fake = new FakeMeasureHistoryStore();
        var p1 = new MeasureHistoryPoint
        {
            Name = "x",
            Timestamp = DateTime.UtcNow,
            NumericValue = 5,
        };
        var p2 = new MeasureHistoryPoint
        {
            Name = "x",
            Timestamp = DateTime.UtcNow.AddMinutes(-1),
            NumericValue = 4,
        };

        await fake.WriteAsync(p1);
        await fake.WriteManyAsync([p2]);

        Assert.Equal(2, fake.WrittenPoints.Count);

        var (svc, _, _) = CreateSut(fake);
        var resp = await svc.Query(new QueryRequest { Name = "x" }, new TestServerCallContext());

        Assert.Equal(2, resp.Points.Count);
    }

    [Fact]
    public async Task Query_store_not_initialized_throws_unavailable()
    {
        var holder = new MeasureHistoryStoreHolder();
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "n" }, new TestServerCallContext()));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task Query_empty_name_throws_invalid_argument()
    {
        var (svc, _, _) = CreateSut();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "  " }, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_aggregation_without_interval_throws()
    {
        var (svc, _, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "x",
            Aggregation = nameof(HistoryAggregation.Average),
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() => svc.Query(req, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_maps_limit_and_aggregation_into_domain_query()
    {
        var (svc, fake, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "z",
            Limit = 10,
            Aggregation = nameof(HistoryAggregation.Max),
            AggregationIntervalMs = 60_000,
        };

        await svc.Query(req, new TestServerCallContext());

        Assert.NotNull(fake.LastQuery);
        Assert.Equal(10, fake.LastQuery!.Limit);
        Assert.Equal(HistoryAggregation.Max, fake.LastQuery.Aggregation);
        Assert.Equal(TimeSpan.FromMinutes(1), fake.LastQuery.AggregationInterval);
    }

    [Fact]
    public async Task Query_NegativeLimit_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "x", Limit = -1 }, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_ZeroLimit_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "x", Limit = 0 }, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_UnknownAggregation_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "x",
            Aggregation = "bogus",
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() => svc.Query(req, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_IntervalWithoutAggregation_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "x",
            AggregationIntervalMs = 60_000,
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() => svc.Query(req, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_AggregationWithoutInterval_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "x",
            Aggregation = "average",
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() => svc.Query(req, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task Query_NegativeInterval_ThrowsInvalidArgument()
    {
        var (svc, _, _) = CreateSut();
        var req = new QueryRequest
        {
            Name = "x",
            Aggregation = "average",
            AggregationIntervalMs = -1,
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() => svc.Query(req, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetDefinitions_StoreNotReady_ThrowsUnavailable()
    {
        var holder = new MeasureHistoryStoreHolder();
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.GetDefinitions(new GetDefinitionsRequest(), new TestServerCallContext()));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task Query_StoreNotReady_ThrowsUnavailable()
    {
        var holder = new MeasureHistoryStoreHolder();
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "n" }, new TestServerCallContext()));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task Query_StoreFails_ThrowsInternal()
    {
        var throwing = new ThrowingQueryMeasureHistoryStore { Exception = new Exception("store boom") };
        var holder = new MeasureHistoryStoreHolder();
        holder.Set(throwing);
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Query(new QueryRequest { Name = "x" }, new TestServerCallContext()));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    [Fact]
    public async Task GetDataRange_returns_earliest_and_latest_unix_ms()
    {
        var (svc, fake, _) = CreateSut();
        var t0 = new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 4, 27, 20, 0, 0, DateTimeKind.Utc);
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "sensor", Timestamp = t0, NumericValue = 1 });
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "sensor", Timestamp = t1, NumericValue = 2 });

        var resp = await svc.GetDataRange(
            new GetDataRangeRequest { Name = "sensor" }, new TestServerCallContext());

        Assert.True(resp.HasEarliestUnixMs);
        Assert.True(resp.HasLatestUnixMs);
        Assert.Equal(new DateTimeOffset(t0).ToUnixTimeMilliseconds(), resp.EarliestUnixMs);
        Assert.Equal(new DateTimeOffset(t1).ToUnixTimeMilliseconds(), resp.LatestUnixMs);
    }

    [Fact]
    public async Task GetDataRange_empty_returns_no_timestamps()
    {
        var (svc, _, _) = CreateSut();

        var resp = await svc.GetDataRange(
            new GetDataRangeRequest { Name = "nonexistent" }, new TestServerCallContext());

        Assert.False(resp.HasEarliestUnixMs);
        Assert.False(resp.HasLatestUnixMs);
    }

    [Fact]
    public async Task GetDataRange_empty_name_throws_invalid_argument()
    {
        var (svc, _, _) = CreateSut();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.GetDataRange(new GetDataRangeRequest { Name = "  " }, new TestServerCallContext()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetDataRange_store_not_ready_throws_unavailable()
    {
        var holder = new MeasureHistoryStoreHolder();
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.GetDataRange(new GetDataRangeRequest { Name = "x" }, new TestServerCallContext()));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task GetDataRange_store_fails_throws_internal()
    {
        var throwing = new ThrowingQueryMeasureHistoryStore { Exception = new Exception("range boom") };
        var holder = new MeasureHistoryStoreHolder();
        holder.Set(throwing);
        var svc = new MeasureHistoryGrpcService(holder, NullLogger<MeasureHistoryGrpcService>.Instance);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.GetDataRange(new GetDataRangeRequest { Name = "x" }, new TestServerCallContext()));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    [Fact]
    public async Task GetDataRange_single_point_returns_same_earliest_and_latest()
    {
        var (svc, fake, _) = CreateSut();
        var ts = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "solo", Timestamp = ts, NumericValue = 42 });

        var resp = await svc.GetDataRange(
            new GetDataRangeRequest { Name = "solo" }, new TestServerCallContext());

        Assert.Equal(resp.EarliestUnixMs, resp.LatestUnixMs);
        Assert.Equal(new DateTimeOffset(ts).ToUnixTimeMilliseconds(), resp.EarliestUnixMs);
    }

    [Fact]
    public async Task GetDataRange_scoped_to_requested_measure()
    {
        var (svc, fake, _) = CreateSut();
        var tA = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tB = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "alpha", Timestamp = tA, NumericValue = 1 });
        await fake.WriteAsync(new MeasureHistoryPoint { Name = "beta", Timestamp = tB, NumericValue = 2 });

        var resp = await svc.GetDataRange(
            new GetDataRangeRequest { Name = "alpha" }, new TestServerCallContext());

        Assert.Equal(new DateTimeOffset(tA).ToUnixTimeMilliseconds(), resp.EarliestUnixMs);
        Assert.Equal(new DateTimeOffset(tA).ToUnixTimeMilliseconds(), resp.LatestUnixMs);
    }
}
