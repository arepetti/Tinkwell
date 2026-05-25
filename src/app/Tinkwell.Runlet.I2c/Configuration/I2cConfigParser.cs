using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.I2c.Configuration;

/// <summary>
/// Parses <c>i2c</c> blocks from a <c>.tw</c> configuration file.
/// </summary>
public sealed class I2cConfigParser : ConfigurationParser<I2cConfig>
{
    public I2cConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<I2cConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var buses = new List<I2cBusDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "i2c", StringComparison.Ordinal))
                continue;

            buses.Add(ParseBus(block));
        }

        return ValueTask.FromResult(new I2cConfig(buses));
    }

    private static I2cBusDefinition ParseBus(ConfigBlock block)
    {
        int busId = 1;
        var pollInterval = TimeSpan.FromSeconds(1);

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "bus":
                    busId = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "poll-interval":
                    var raw = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    pollInterval = ParseDuration(raw, prop.Location);
                    break;
            }
        }

        var devices = new List<I2cDeviceDefinition>();
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "device", StringComparison.Ordinal))
                devices.Add(ParseDevice(child));
        }

        return new I2cBusDefinition(block.Name, busId, pollInterval, devices);
    }

    private static I2cDeviceDefinition ParseDevice(ConfigBlock block)
    {
        var address = ParseAddress(block.Name);

        var reads = new List<I2cReadDefinition>();
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "read", StringComparison.Ordinal))
                reads.Add(ParseRead(child));
        }

        return new I2cDeviceDefinition(address, reads);
    }

    private static I2cReadDefinition ParseRead(ConfigBlock block)
    {
        byte register = 0;
        int length = 0;
        var dataType = I2cDataType.UInt8;
        double scale = 1.0;
        string? measureName = null;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "register":
                    register = (byte)ParseAddress(
                        ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location));
                    break;
                case "length":
                    length = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "type":
                    var typeStr = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    dataType = ParseDataType(typeStr, prop.Location);
                    break;
                case "scale":
                    scale = ConfigValueConverter.ConvertTo<double>(prop.Value, prop.Location);
                    break;
                case "measure":
                    measureName = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
            }
        }

        if (length == 0)
            length = DefaultLength(dataType);

        return new I2cReadDefinition(
            block.Name, register, length, dataType, scale, measureName ?? block.Name);
    }

    private static int ParseAddress(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt32(text[2..], 16);
        return int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static I2cDataType ParseDataType(string text, SourceLocation location) =>
        text.ToLowerInvariant() switch
        {
            "int8" => I2cDataType.Int8,
            "uint8" or "byte" => I2cDataType.UInt8,
            "int16-be" => I2cDataType.Int16BE,
            "int16-le" => I2cDataType.Int16LE,
            "uint16-be" => I2cDataType.UInt16BE,
            "uint16-le" => I2cDataType.UInt16LE,
            "int32-be" => I2cDataType.Int32BE,
            "int32-le" => I2cDataType.Int32LE,
            "float32-be" => I2cDataType.Float32BE,
            "float32-le" => I2cDataType.Float32LE,
            _ => throw new ConfigurationSyntaxException(
                $"Unknown I2C data type '{text}'.", location.FilePath, location.Line, location.Column),
        };

    private static int DefaultLength(I2cDataType type) => type switch
    {
        I2cDataType.Int8 or I2cDataType.UInt8 => 1,
        I2cDataType.Int16BE or I2cDataType.Int16LE
            or I2cDataType.UInt16BE or I2cDataType.UInt16LE => 2,
        _ => 4,
    };

    private static TimeSpan ParseDuration(string raw, SourceLocation location)
    {
        if (double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var val))
        {
            return parts[1].ToLowerInvariant() switch
            {
                "ms" or "millisecond" or "milliseconds" => TimeSpan.FromMilliseconds(val),
                "s" or "second" or "seconds" => TimeSpan.FromSeconds(val),
                "m" or "minute" or "minutes" => TimeSpan.FromMinutes(val),
                _ => throw new ConfigurationSyntaxException(
                    $"Unknown duration unit in '{raw}'.", location.FilePath, location.Line, location.Column),
            };
        }

        if (raw.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^2].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var ms))
            return TimeSpan.FromMilliseconds(ms);

        if (raw.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var s))
            return TimeSpan.FromSeconds(s);

        throw new ConfigurationSyntaxException(
            $"Cannot parse duration '{raw}'.", location.FilePath, location.Line, location.Column);
    }
}
