using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Modbus;

namespace Tinkwell.Runlet.Modbus.Configuration;

/// <summary>
/// Parses <c>modbus</c> blocks from a <c>.tw</c> configuration file.
/// </summary>
public sealed class ModbusConfigParser : ConfigurationParser<ModbusConfig>
{
    public ModbusConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<ModbusConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var connections = new List<ModbusConnectionDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "modbus", StringComparison.Ordinal))
                continue;

            connections.Add(ParseConnection(block));
        }

        return ValueTask.FromResult(new ModbusConfig(connections));
    }

    private static ModbusConnectionDefinition ParseConnection(ConfigBlock block)
    {
        var transport = ModbusTransport.Rtu;
        string? port = null;
        int baudRate = 9600;
        string? host = null;
        int tcpPort = 502;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "transport":
                    var val = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    transport = val.ToLowerInvariant() switch
                    {
                        "rtu" or "serial" => ModbusTransport.Rtu,
                        "tcp" => ModbusTransport.Tcp,
                        _ => throw new ConfigurationSyntaxException(
                            $"Unknown modbus transport '{val}'. Expected 'rtu' or 'tcp'.",
                            prop.Location.FilePath, prop.Location.Line, prop.Location.Column),
                    };
                    break;
                case "port":
                    port = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "baudrate":
                    baudRate = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "host":
                    host = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "tcp-port":
                    tcpPort = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
            }
        }

        var devices = new List<ModbusDeviceDefinition>();
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "device", StringComparison.Ordinal))
                devices.Add(ParseDevice(child));
        }

        return new ModbusConnectionDefinition(block.Name, transport, port, baudRate, host, tcpPort, devices);
    }

    private static ModbusDeviceDefinition ParseDevice(ConfigBlock block)
    {
        if (!byte.TryParse(block.Name, out var slaveId))
            throw new ConfigurationSyntaxException(
                $"Device name must be a numeric slave ID, got '{block.Name}'.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);

        var pollInterval = TimeSpan.FromSeconds(1);

        foreach (var prop in block.Properties)
        {
            if (prop.Key == "poll-interval")
            {
                var raw = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                pollInterval = ParseDuration(raw, prop.Location);
            }
        }

        var registers = new List<ModbusRegisterDefinition>();
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "register", StringComparison.Ordinal))
                registers.Add(ParseRegister(child));
        }

        return new ModbusDeviceDefinition(slaveId, pollInterval, registers);
    }

    private static ModbusRegisterDefinition ParseRegister(ConfigBlock block)
    {
        ushort address = 0;
        var dataType = ModbusDataType.Int16;
        var kind = ModbusRegisterKind.Holding;
        double scale = 1.0;
        string? measureName = null;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "address":
                    address = ParseAddress(
                        ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location), prop.Location);
                    break;
                case "type":
                    dataType = ParseDataType(
                        ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location), prop.Location);
                    break;
                case "kind":
                    var k = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    kind = k.ToLowerInvariant() switch
                    {
                        "holding" => ModbusRegisterKind.Holding,
                        "input" => ModbusRegisterKind.Input,
                        _ => throw new ConfigurationSyntaxException(
                            $"Unknown register kind '{k}'. Expected 'holding' or 'input'.",
                            prop.Location.FilePath, prop.Location.Line, prop.Location.Column),
                    };
                    break;
                case "scale":
                    scale = ConfigValueConverter.ConvertTo<double>(prop.Value, prop.Location);
                    break;
                case "measure":
                    measureName = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
            }
        }

        return new ModbusRegisterDefinition(
            block.Name, address, dataType, kind, scale, measureName ?? block.Name);
    }

    private static ushort ParseAddress(string raw, SourceLocation location)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ushort.TryParse(raw.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
            return hex;

        if (ushort.TryParse(raw, out var dec))
            return dec;

        throw new ConfigurationSyntaxException(
            $"Invalid register address '{raw}'.",
            location.FilePath, location.Line, location.Column);
    }

    private static ModbusDataType ParseDataType(string raw, SourceLocation location) =>
        raw.ToLowerInvariant() switch
        {
            "int16" => ModbusDataType.Int16,
            "uint16" => ModbusDataType.UInt16,
            "int32-be" or "int32" => ModbusDataType.Int32BigEndian,
            "int32-le" => ModbusDataType.Int32LittleEndian,
            "uint32-be" or "uint32" => ModbusDataType.UInt32BigEndian,
            "uint32-le" => ModbusDataType.UInt32LittleEndian,
            "float32-be" or "float32" or "float" => ModbusDataType.Float32BigEndian,
            "float32-le" => ModbusDataType.Float32LittleEndian,
            "float32-ws" or "float32-swapped" => ModbusDataType.Float32WordSwapped,
            _ => throw new ConfigurationSyntaxException(
                $"Unknown register data type '{raw}'.",
                location.FilePath, location.Line, location.Column),
        };

    private static TimeSpan ParseDuration(string raw, SourceLocation location)
    {
        if (double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        if (raw.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^2].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var ms))
            return TimeSpan.FromMilliseconds(ms);

        if (raw.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(raw[..^1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var s))
            return TimeSpan.FromSeconds(s);

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

        throw new ConfigurationSyntaxException(
            $"Cannot parse duration '{raw}'.", location.FilePath, location.Line, location.Column);
    }
}
