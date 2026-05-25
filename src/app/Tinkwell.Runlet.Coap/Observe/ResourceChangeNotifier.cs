using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Tinkwell.Runlet.Coap.Observe;

/// <summary>
/// Buffers resource-change signals from bindings and exposes them as
/// a channel for the <see cref="ObserverNotifier"/> to consume.
/// </summary>
internal sealed class ResourceChangeNotifier : IResourceChangeNotifier
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    private readonly ChannelDropTracker _dropTracker;

    public ResourceChangeNotifier(ILogger logger)
    {
        _dropTracker = new ChannelDropTracker("coap.resource-changes", logger);
    }

    public void NotifyChanged(string path) =>
        _dropTracker.TryWrite(_channel.Writer, path);

    public ChannelReader<string> Reader => _channel.Reader;
}
