using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Modbus;

namespace Tinkwell.Cli.Commands.Modbus;

public sealed class ModbusWriteSettings : TwSettings
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

    [Description("Serial port (for rtu transport)")]
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

    [Description("Register address (decimal or 0x hex)")]
    [CommandArgument(0, "<address>")]
    public string Address { get; set; } = "0";

    [Description("Value to write (unsigned 16-bit integer)")]
    [CommandArgument(1, "<value>")]
    public ushort Value { get; set; }
}

[CliCommand("modbus", "write", Description = "Write a single register to a Modbus device")]
public sealed class ModbusWriteCommand : AsyncCommand<ModbusWriteSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, ModbusWriteSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var address = ParseAddress(settings.Address);
            await using var client = CreateClient(settings);
            await client.ConnectAsync(ct);

            await client.WriteSingleRegisterAsync(settings.SlaveId, address, settings.Value, ct);

            if (output.Format == OutputFormat.Jsonl)
            {
                var json = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    address = $"0x{address:X4}",
                    value = settings.Value,
                }, JsonOpts);
                Console.WriteLine(json);
            }
            else
            {
                output.WriteSuccess(
                    $"Wrote [cyan]{settings.Value}[/] to register [cyan]0x{address:X4}[/] on slave {settings.SlaveId}");
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

    private static IModbusClient CreateClient(ModbusWriteSettings settings) =>
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
}