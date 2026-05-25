using System.Text.Json;
using Grpc.Net.Client;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Cli;

/// <summary>
/// Discovers the state store's gRPC endpoint via the coordinator pipe
/// and creates a typed client. Each call to <see cref="ConnectAsync"/>
/// discovers fresh and returns a disposable handle.
/// </summary>
internal static class StoreClient
{
    private const string ServiceFamily = "store";

    /// <summary>
    /// Discovers the store and returns a connected handle. Dispose to
    /// release the channel.
    /// </summary>
    public static async Task<StoreHandle> ConnectAsync(
        TwCoordinatorSettings settings, CancellationToken ct = default)
    {
        var data = await PipeCommandRunner.SendOkAsync(
            settings, $"service find {ServiceFamily}", ct);

        if (data is not { ValueKind: JsonValueKind.Object } obj)
            throw new TwCommandException("State store service not found");

        var url = obj.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("State store service has no URL");

        var channel = GrpcChannel.ForAddress(url);
        var client = new StateStore.StateStoreClient(channel);

        return new StoreHandle(client, channel);
    }
}

/// <summary>
/// A connected gRPC client to the state store. Dispose to release the
/// underlying channel.
/// </summary>
internal sealed class StoreHandle : IDisposable
{
    public StateStore.StateStoreClient Client { get; }
    private readonly GrpcChannel _channel;

    public StoreHandle(StateStore.StateStoreClient client, GrpcChannel channel)
    {
        Client = client;
        _channel = channel;
    }

    public void Dispose() => _channel.Dispose();
}
