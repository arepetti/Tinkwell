using System.Text.Json;
using Grpc.Net.Client;
using EventsGrpc = Tinkwell.Runlet.Events.Grpc.V1;

namespace Tinkwell.Cli.Commands.Events;

internal static class EventsHelper
{
    private const string ServiceFamily = "events";

    public static async Task<EventsHandle> ConnectAsync(
        EventsSettings settings, CancellationToken ct)
    {
        var data = await PipeCommandRunner.SendOkAsync(
            settings, $"service find {ServiceFamily}", ct);

        if (data is not { ValueKind: JsonValueKind.Object } obj)
            throw new TwCommandException("Events service not found");

        var url = obj.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("Events service has no URL");

        var channel = GrpcChannel.ForAddress(url);
        var client = new EventsGrpc.EventBus.EventBusClient(channel);

        return new EventsHandle(client, channel);
    }
}

internal sealed class EventsHandle : IDisposable
{
    public EventsGrpc.EventBus.EventBusClient Client { get; }
    private readonly GrpcChannel _channel;

    public EventsHandle(EventsGrpc.EventBus.EventBusClient client, GrpcChannel channel)
    {
        Client = client;
        _channel = channel;
    }

    public void Dispose() => _channel.Dispose();
}
