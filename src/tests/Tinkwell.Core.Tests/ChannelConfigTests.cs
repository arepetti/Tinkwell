using System.Threading.Channels;
using Tinkwell;

namespace Tinkwell.Core.Tests;

public class ChannelConfigTests
{
    [Fact]
    public void ToBoundedOptions_RespectsCapacityAndMode()
    {
        var cfg = new ChannelConfig(64, BoundedChannelFullMode.Wait);
        var o = cfg.ToBoundedOptions();
        Assert.Equal(64, o.Capacity);
        Assert.Equal(BoundedChannelFullMode.Wait, o.FullMode);
        Assert.True(o.SingleReader);
        Assert.False(o.SingleWriter);
    }

    [Fact]
    public void ToBoundedOptions_CanDisableSingleReader()
    {
        var o = new ChannelConfig(2, BoundedChannelFullMode.DropWrite).ToBoundedOptions(singleReader: false);
        Assert.False(o.SingleReader);
    }
}
