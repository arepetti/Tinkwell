using System.Threading.Channels;

namespace Tinkwell;

/// <summary>
/// Configurable bounded-channel parameters. Each consumer should define
/// sensible defaults; these can be overridden from the <c>.tw</c> file.
/// </summary>
public sealed record ChannelConfig(int Capacity, BoundedChannelFullMode FullMode)
{
    /// <summary>Creates <see cref="BoundedChannelOptions"/> from this configuration.</summary>
    public BoundedChannelOptions ToBoundedOptions(
        bool singleReader = true, bool singleWriter = false) =>
        new(Capacity)
        {
            FullMode = FullMode,
            SingleReader = singleReader,
            SingleWriter = singleWriter,
        };
}
