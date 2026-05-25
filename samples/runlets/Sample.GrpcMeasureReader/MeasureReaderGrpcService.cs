using Grpc.Core;
using Sample.GrpcMeasureReader.Grpc;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc;

namespace Sample.GrpcMeasureReader;

/// <summary>
/// gRPC service that reads a single measure from the Tinkwell Measures service
/// via gRPC (cross-runner). The measures service is discovered at first call
/// through <see cref="IServiceDiscovery"/> and the client is cached for reuse.
/// </summary>
internal sealed class MeasureReaderGrpcService(
    IServiceDiscovery discovery,
    MeasureReaderOptions options)
    : MeasureReader.MeasureReaderBase
{
    private MeasuresGrpc.Measures.MeasuresClient? _client;

    public override async Task<ReadResponse> Read(ReadRequest request, ServerCallContext context)
    {
        var client = await GetClientAsync(context.CancellationToken);
        if (client is null)
        {
            return new ReadResponse
            {
                Name = options.MeasureName,
                Found = false,
            };
        }

        var reply = await client.GetAsync(
            new MeasuresGrpc.GetMeasureRequest { Name = options.MeasureName },
            cancellationToken: context.CancellationToken);

        if (!reply.Found || reply.Measure is null)
        {
            return new ReadResponse
            {
                Name = options.MeasureName,
                Found = false,
            };
        }

        var val = reply.Measure.Value;
        return new ReadResponse
        {
            Name = options.MeasureName,
            Value = val?.NumericValue ?? 0,
            Display = val?.StringValue ?? val?.NumericValue.ToString() ?? "",
            Found = true,
        };
    }

    private async Task<MeasuresGrpc.Measures.MeasuresClient?> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        var svc = await discovery.DiscoverAsync("measures", ct);

        if (svc is null)
            return null;

        var client = await discovery.CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, ct);
        Interlocked.CompareExchange(ref _client, client, null);
        return _client;
    }
}
