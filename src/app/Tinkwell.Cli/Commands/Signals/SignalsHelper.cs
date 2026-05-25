using System.Text.Json;
using Grpc.Net.Client;
using SignalsGrpc = Tinkwell.Runlet.Signals.Grpc.V1;

namespace Tinkwell.Cli.Commands.Signals;

/// <summary>
/// Discovers the Signals gRPC service and returns a connected client handle.
/// </summary>
internal static class SignalsHelper
{
    private const string ServiceFamily = "signals";

    public static async Task<SignalsHandle> ConnectAsync(
        SignalsSettings settings, CancellationToken ct)
    {
        var data = await PipeCommandRunner.SendOkAsync(
            settings, $"service find {ServiceFamily}", ct);

        if (data is not { ValueKind: JsonValueKind.Object } obj)
            throw new TwCommandException("Signals service not found");

        var url = obj.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("Signals service has no URL");

        var channel = GrpcChannel.ForAddress(url);
        var client = new SignalsGrpc.Signals.SignalsClient(channel);

        return new SignalsHandle(client, channel);
    }
}

internal sealed class SignalsHandle : IDisposable
{
    public SignalsGrpc.Signals.SignalsClient Client { get; }
    private readonly GrpcChannel _channel;

    public SignalsHandle(SignalsGrpc.Signals.SignalsClient client, GrpcChannel channel)
    {
        Client = client;
        _channel = channel;
    }

    public void Dispose() => _channel.Dispose();
}
