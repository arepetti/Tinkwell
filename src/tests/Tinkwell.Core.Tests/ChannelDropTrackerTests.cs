using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell;

namespace Tinkwell.Core.Tests;

public class ChannelDropTrackerTests
{
    [Fact]
    public void TryWrite_WhenFull_IncrementsDroppedAndReturnsFalse()
    {
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        };
        var channel = Channel.CreateBounded<int>(options);
        var tracker = new ChannelDropTracker("test-chan", NullLogger.Instance);

        Assert.True(tracker.TryWrite(channel.Writer, 1));
        Assert.False(tracker.TryWrite(channel.Writer, 2));
        Assert.False(tracker.TryWrite(channel.Writer, 3));

        Assert.Equal(2, tracker.Dropped);
    }
}
