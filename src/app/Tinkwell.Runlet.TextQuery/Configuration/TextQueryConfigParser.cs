using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.TextQuery.Configuration;

/// <summary>
/// Parses <c>query</c> blocks from a <c>.tw</c> configuration file.
/// </summary>
/// <remarks>
/// Expected syntax:
/// <code>
/// query my-instrument {
///     transport = tcp
///     host = "192.168.1.50"
///     port = 5025
///     poll-interval = "1 second"
///
///     read voltage {
///         send = "MEAS:VOLT:DC?"
///         pattern = "([+-]?[0-9.]+)"
///         measure = board-voltage
///     }
/// }
/// </code>
/// </remarks>
public sealed class TextQueryConfigParser : ConfigurationParser<TextQueryConfig>
{
    public TextQueryConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<TextQueryConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var sources = new List<TextQuerySourceDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "query", StringComparison.Ordinal))
                continue;

            sources.Add(ParseSource(block));
        }

        return ValueTask.FromResult(new TextQueryConfig(sources));
    }

    private static TextQuerySourceDefinition ParseSource(ConfigBlock block)
    {
        var transport = TextQueryTransport.Tcp;
        string? host = null;
        int tcpPort = 5025;
        string? serialPort = null;
        int baudRate = 9600;
        string? filePath = null;
        string? command = null;
        var lineTerminator = "\n";
        int readTimeoutMs = 2000;
        var pollInterval = TimeSpan.FromSeconds(1);

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "transport":
                    var val = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    transport = val.ToLowerInvariant() switch
                    {
                        "tcp" => TextQueryTransport.Tcp,
                        "serial" or "rtu" => TextQueryTransport.Serial,
                        "file" => TextQueryTransport.File,
                        "command" or "cmd" or "exec" => TextQueryTransport.Command,
                        _ => throw new ConfigurationSyntaxException(
                            $"Unknown query transport '{val}'. Expected 'tcp', 'serial', 'file', or 'command'.",
                            prop.Location.FilePath, prop.Location.Line, prop.Location.Column),
                    };
                    break;
                case "host":
                    host = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "port":
                    tcpPort = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "serial-port":
                    serialPort = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "baudrate":
                    baudRate = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "path":
                    filePath = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "command":
                    command = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "line-terminator":
                    var ltRaw = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    lineTerminator = ResolveLineTerminator(ltRaw, prop.Location);
                    break;
                case "read-timeout":
                    readTimeoutMs = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "poll-interval":
                    var raw = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    pollInterval = ParseDuration(raw, prop.Location);
                    break;
            }
        }

        var reads = new List<TextQueryReadDefinition>();
        foreach (var child in block.Children)
        {
            if (string.Equals(child.Type, "read", StringComparison.Ordinal))
                reads.Add(ParseRead(child));
        }

        return new TextQuerySourceDefinition(
            block.Name, transport, host, tcpPort, serialPort, baudRate,
            filePath, command, lineTerminator, readTimeoutMs, pollInterval, reads);
    }

    private static TextQueryReadDefinition ParseRead(ConfigBlock block)
    {
        string? sendCommand = null;
        string? pattern = null;
        int captureGroup = 1;
        double scale = 1.0;
        string? measureName = null;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "send":
                    sendCommand = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "pattern":
                    pattern = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
                case "group":
                    captureGroup = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "scale":
                    scale = ConfigValueConverter.ConvertTo<double>(prop.Value, prop.Location);
                    break;
                case "measure":
                    measureName = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                    break;
            }
        }

        if (pattern is null)
            throw new ConfigurationSyntaxException(
                $"Read block '{block.Name}' requires a 'pattern' property.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);

        return new TextQueryReadDefinition(
            block.Name, sendCommand, pattern, captureGroup, scale, measureName ?? block.Name);
    }

    private static string ResolveLineTerminator(string name, SourceLocation location) =>
        name.ToLowerInvariant() switch
        {
            "lf" => "\n",
            "cr" => "\r",
            "crlf" => "\r\n",
            "none" => "",
            _ => throw new ConfigurationSyntaxException(
                $"Unknown line-terminator '{name}'. Use cr, lf, crlf, or none.",
                location.FilePath, location.Line, location.Column),
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
