using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures.History;

namespace Tinkwell.Runlet.MeasureHistory.Grpc.V1;

internal sealed class MeasureHistoryGrpcService :
    global::Tinkwell.Runlet.MeasureHistory.Grpc.V1.MeasureHistory.MeasureHistoryBase
{
    private readonly MeasureHistoryStoreHolder _holder;
    private readonly ILogger<MeasureHistoryGrpcService> _logger;

    public MeasureHistoryGrpcService(
        MeasureHistoryStoreHolder holder,
        ILogger<MeasureHistoryGrpcService> logger)
    {
        _holder = holder;
        _logger = logger;
    }

    public override async Task<QueryResponse> Query(QueryRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Query requires a non-empty name."));
        }

        var store = GetStore();
        var query = ToDomainQuery(request);

        if (query.Aggregation is { } agg && agg is not HistoryAggregation.None)
        {
            if (query.AggregationInterval is not { } interval)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "aggregation_interval_ms is required when aggregation is set."));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "aggregation_interval_ms must be positive."));
            }
        }

        try
        {
            var result = await store.QueryAsync(query, context.CancellationToken);
            return ToProto(result);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Measure history query failed for '{Name}'", request.Name);
            throw new RpcException(new Status(
                StatusCode.Internal, "Query failed."));
        }
    }

    public override async Task<GetDefinitionsResponse> GetDefinitions(
        GetDefinitionsRequest request,
        ServerCallContext context)
    {
        _ = request;
        var store = GetStore();

        try
        {
            var defs = await store.GetDefinitionsAsync(context.CancellationToken);
            var response = new GetDefinitionsResponse();
            foreach (var d in defs)
                response.Definitions.Add(ToProto(d));

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDefinitions failed");
            throw new RpcException(new Status(
                StatusCode.Internal, "GetDefinitions failed."));
        }
    }

    public override async Task<GetDataRangeResponse> GetDataRange(
        GetDataRangeRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "GetDataRange requires a non-empty name."));
        }

        var store = GetStore();

        try
        {
            var range = await store.GetDataRangeAsync(request.Name.Trim(), context.CancellationToken);
            var response = new GetDataRangeResponse();

            if (range.Earliest is { } earliest)
            {
                response.EarliestUnixMs = new DateTimeOffset(
                    DateTime.SpecifyKind(earliest, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            }

            if (range.Latest is { } latest)
            {
                response.LatestUnixMs = new DateTimeOffset(
                    DateTime.SpecifyKind(latest, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            }

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDataRange failed for '{Name}'", request.Name);
            throw new RpcException(new Status(
                StatusCode.Internal, "GetDataRange failed."));
        }
    }

    private IMeasureHistoryStore GetStore() =>
        _holder.Store
        ?? throw new RpcException(new Status(
            StatusCode.Unavailable, "Measure history store is not yet initialized."));

    private static MeasureHistoryQuery ToDomainQuery(QueryRequest request)
    {
        DateTime? from = request.HasFromUnixMs
            ? DateTimeOffset.FromUnixTimeMilliseconds(request.FromUnixMs).UtcDateTime
            : null;
        DateTime? to = request.HasToUnixMs
            ? DateTimeOffset.FromUnixTimeMilliseconds(request.ToUnixMs).UtcDateTime
            : null;
        int? limit = request.HasLimit ? request.Limit : null;
        if (limit is <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "limit must be positive."));
        }

        HistoryAggregation? aggregation = null;
        TimeSpan? aggregationInterval = null;
        if (request.HasAggregation && !string.IsNullOrWhiteSpace(request.Aggregation))
        {
            if (!Enum.TryParse<HistoryAggregation>(request.Aggregation, ignoreCase: true, out var agg))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"Unknown aggregation '{request.Aggregation}'."));
            }

            aggregation = agg;
            if (request.HasAggregationIntervalMs)
                aggregationInterval = TimeSpan.FromMilliseconds(request.AggregationIntervalMs);
        }
        else if (request.HasAggregationIntervalMs)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "aggregation must be set when aggregation_interval_ms is provided."));
        }

        return new MeasureHistoryQuery
        {
            Name = request.Name.Trim(),
            From = from,
            To = to,
            Limit = limit,
            Aggregation = aggregation,
            AggregationInterval = aggregationInterval,
        };
    }

    private static QueryResponse ToProto(MeasureHistoryResult result)
    {
        var response = new QueryResponse { HasMore = result.HasMore };
        foreach (var p in result.Points)
            response.Points.Add(ToProto(p));

        return response;
    }

    private static HistoryPoint ToProto(MeasureHistoryPoint p)
    {
        var proto = new HistoryPoint
        {
            Name = p.Name,
            TimestampUnixMs = new DateTimeOffset(
                DateTime.SpecifyKind(p.Timestamp, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            Unit = p.Unit ?? "",
        };

        if (p.NumericValue is { } n)
            proto.NumericValue = n;

        if (p.StringValue is { } s)
            proto.StringValue = s;

        if (p.OpaqueValue is { } opaque && opaque.Length > 0)
            proto.OpaqueValue = UnsafeByteOperations.UnsafeWrap(opaque);

        return proto;
    }

    private static HistoryDefinitionSnapshot ToProto(MeasureDefinitionSnapshot d)
    {
        var proto = new HistoryDefinitionSnapshot
        {
            Name = d.Name,
            Type = d.Type,
            QuantityType = d.QuantityType ?? "",
            Unit = d.Unit ?? "",
            Description = d.Description ?? "",
            Category = d.Category ?? "",
        };

        if (d.Minimum is { } min)
            proto.Minimum = min;
        if (d.Maximum is { } max)
            proto.Maximum = max;
        if (d.Precision is { } prec)
            proto.Precision = prec;

        foreach (var tag in d.Tags)
            proto.Tags.Add(tag);

        return proto;
    }
}