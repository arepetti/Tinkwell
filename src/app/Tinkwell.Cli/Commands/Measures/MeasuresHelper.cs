using System.Text.Json;
using Grpc.Net.Client;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Cli.Commands.Measures;

/// <summary>
/// Discovers the Measures gRPC service and returns a connected client handle.
/// </summary>
internal static class MeasuresHelper
{
    private const string ServiceFamily = "measures";

    public static async Task<MeasuresHandle> ConnectAsync(
        MeasuresSettings settings, CancellationToken ct)
    {
        var data = await PipeCommandRunner.SendOkAsync(
            settings, $"service find {ServiceFamily}", ct);

        if (data is not { ValueKind: JsonValueKind.Object } obj)
            throw new TwCommandException("Measures service not found");

        var url = obj.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("Measures service has no URL");

        var channel = GrpcChannel.ForAddress(url);
        var client = new MeasuresGrpc.Measures.MeasuresClient(channel);

        return new MeasuresHandle(client, channel);
    }
}

internal sealed class MeasuresHandle : IDisposable
{
    public MeasuresGrpc.Measures.MeasuresClient Client { get; }
    private readonly GrpcChannel _channel;

    public MeasuresHandle(MeasuresGrpc.Measures.MeasuresClient client, GrpcChannel channel)
    {
        Client = client;
        _channel = channel;
    }

    public void Dispose() => _channel.Dispose();
}
