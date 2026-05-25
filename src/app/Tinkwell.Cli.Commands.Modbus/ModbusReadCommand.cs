using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Modbus;

namespace Tinkwell.Cli.Commands.Modbus;

public sealed class ModbusReadSettings : TwSettings
{
    [Description("Transport: rtu or tcp")]
    [CommandOption("--transport|-t")]
    [DefaultValue("tcp")]
    public string Transport { get; set; } = "tcp";

    [Description("TCP host (for tcp transport)")]
    [CommandOption("--host")]
    [DefaultValue("localhost")]
    public string Host { get; set; } = "localhost";

    [Description("TCP port (for tcp transport)")]
    [CommandOption("--tcp-port")]
    [DefaultValue(502)]
    public int TcpPort { get; set; } = 502;

    [Description("Serial port (for rtu transport, e.g. /dev/ttyUSB0 or COM3)")]
    [CommandOption("--port")]
    public string? Port { get; set; }

    [Description("Baud rate (for rtu transport)")]
    [CommandOption("--baudrate")]
    [DefaultValue(9600)]
    public int BaudRate { get; set; } = 9600;

    [Description("Modbus slave ID")]
    [CommandOption("--slave|-s")]
    [DefaultValue((byte)1)]
    public byte SlaveId { get; set; } = 1;

    [Description("Starting register address (decimal or 0x hex)")]
    [CommandArgument(0, "<address>")]
    public string Address { get; set; } = "0";

    [Description("Number of registers to read")]
    [CommandOption("--count|-c")]
    [DefaultValue((ushort)1)]
    public ushort Count { get; set; } = 1;

    [Description("Data type for decoding: int16, uint16, float32-be, etc.")]
    [CommandOption("--type")]
    public string? DataType { get; set; }

    [Description("Scale factor applied to the decoded value")]
    [CommandOption("--scale")]
    [DefaultValue(1.0)]
    public double Scale { get; set; } = 1.0;

    [Description("Read input registers (FC 04) instead of holding registers (FC 03)")]
    [CommandOption("--input")]
    [DefaultValue(false)]
    public bool InputRegisters { get; set; }
}

[CliCommand("modbus", "read", Description = "Read registers from a Modbus device")]
public sealed class ModbusReadCommand : AsyncCommand<ModbusReadSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, ModbusReadSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var address = ParseAddress(settings.Address);
            await using var client = CreateClient(settings);
            await client.ConnectAsync(ct);

            var registers = settings.InputRegisters
                ? await client.ReadInputRegistersAsync(settings.SlaveId, address, settings.Count, ct)
                : await client.ReadHoldingRegistersAsync(settings.SlaveId, address, settings.Count, ct);

            if (settings.DataType is not null)
            {
                var dataType = ParseDataType(settings.DataType);
                var value = RegisterDecoder.Decode(registers, dataType, settings.Scale);

                if (output.Format == OutputFormat.Jsonl)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        address = $"0x{address:X4}",
                        type = settings.DataType,
                        value,
                        raw = registers.Select(r => $"0x{r:X4}").ToArray(),
                    }, JsonOpts);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[dim]Address 0x{address:X4}:[/] [cyan]{value}[/] [dim]({settings.DataType})[/]");
                }
            }
            else
            {
                if (output.Format == OutputFormat.Jsonl)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        address = $"0x{address:X4}",
                        registers = registers.Select(r => $"0x{r:X4}").ToArray(),
                    }, JsonOpts);
                    Console.WriteLine(json);
                }
                else
                {
                    for (int i=0; i < registers.Length; ++i)
                    {
                        AnsiConsole.MarkupLine(
                            $"  [dim]0x{(address + i):X4}:[/] [cyan]0x{registers[i]:X4}[/] [dim]({registers[i]})[/]");
                    }
                }
            }

            return 0;
        }
        catch (ModbusException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    private static IModbusClient CreateClient(ModbusReadSettings settings) =>
        settings.Transport.ToLowerInvariant() switch
        {
            "tcp" => new ModbusTcpClient(settings.Host, settings.TcpPort),
            "rtu" or "serial" => new ModbusRtuClient(
                settings.Port ?? throw new TwCommandException("--port is required for RTU transport"),
                settings.BaudRate),
            _ => throw new TwCommandException($"Unknown transport '{settings.Transport}'"),
        };

    private static ushort ParseAddress(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ushort.TryParse(raw.AsSpan(2), NumberStyles.HexNumber, null, out var hex))
            return hex;
        if (ushort.TryParse(raw, out var dec))
            return dec;
        throw new TwCommandException($"Invalid register address '{raw}'");
    }

    private static ModbusDataType ParseDataType(string raw) =>
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
            _ => throw new TwCommandException($"Unknown data type '{raw}'"),
        };
}