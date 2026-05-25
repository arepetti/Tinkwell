using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;

namespace Tinkwell.Runlet.Actions.Handlers;

/// <summary>
/// Built-in handler that sends a text payload over TCP, serial, or to a file
/// when an action fires. Designed as the write counterpart of the TextQuery runlet.
/// </summary>
/// <remarks>
/// <para>Parameters:</para>
/// <list type="bullet">
///   <item><c>transport</c> (required) — <c>tcp</c>, <c>serial</c>, or <c>file</c>.</item>
///   <item><c>host</c> (TCP) — hostname or IP address.</item>
///   <item><c>port</c> (TCP) — TCP port. Defaults to <c>5025</c>.</item>
///   <item><c>serial-port</c> (serial) — port name (e.g. <c>COM3</c>, <c>/dev/ttyUSB0</c>).</item>
///   <item><c>baudrate</c> (serial) — baud rate. Defaults to <c>9600</c>.</item>
///   <item><c>path</c> (file) — absolute file path to write to.</item>
///   <item><c>send</c> (required) — content to transmit. Supports expressions.</item>
///   <item><c>line-terminator</c> (optional) — <c>lf</c> (default), <c>cr</c>,
///     <c>crlf</c>, or <c>none</c>. Appended after <c>send</c>.</item>
/// </list>
/// <para>
/// <b>Security:</b> The <c>file</c> transport writes directly to the filesystem.
/// On embedded Linux (Raspberry Pi, etc.) this is the standard way to control
/// GPIO pins and device files via sysfs. Ensure the Tinkwell process runs with
/// appropriate permissions and restrict file paths via OS-level controls (file
/// permissions, AppArmor/SELinux, or container bind mounts). The <c>command</c>
/// transport is not supported.
/// </para>
/// </remarks>
internal sealed class TextSendActionHandler : IActionHandler
{
    private readonly ILogger<TextSendActionHandler> _logger;

    public TextSendActionHandler(ILogger<TextSendActionHandler> logger) => _logger = logger;

    public string Name => "text-send";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var transport = await ActionParameterResolver.ResolveRequiredAsync(
            "transport", parameters, trigger, evaluator, ct);

        var command = await ActionParameterResolver.ResolveRequiredAsync(
            "send", parameters, trigger, evaluator, ct);

        var terminatorName = (await ActionParameterResolver.ResolveOptionalAsync(
            "line-terminator", parameters, trigger, evaluator, ct))
            ?? "lf";

        var terminator = ResolveLineTerminator(terminatorName);
        var payload = command + terminator;

        switch (transport.ToLowerInvariant())
        {
            case "tcp":
                await SendTcpAsync(parameters, trigger, evaluator, payload, ct);
                break;
            case "serial":
                await SendSerialAsync(parameters, trigger, evaluator, payload, ct);
                break;
            case "file":
                await SendFileAsync(parameters, trigger, evaluator, payload, ct);
                break;
            default:
                _logger.LogError(
                    "text-send: unsupported transport '{Transport}'. Use 'tcp', 'serial', or 'file'.",
                    transport);
                break;
        }
    }

    private async Task SendTcpAsync(
        IReadOnlyDictionary<string, ConfigValue> parameters,
        EventEnvelope trigger,
        IExpressionEvaluator evaluator,
        string payload,
        CancellationToken ct)
    {
        var host = await ActionParameterResolver.ResolveRequiredAsync(
            "host", parameters, trigger, evaluator, ct);

        var portStr = await ActionParameterResolver.ResolveOptionalAsync(
            "port", parameters, trigger, evaluator, ct)
            ?? "5025";

        if (!int.TryParse(portStr, out var port))
        {
            _logger.LogError("text-send: invalid port '{Port}'", portStr);
            return;
        }

        _logger.LogDebug("text-send: TCP {Host}:{Port} <- {Command}",
            host, port, payload.TrimEnd());

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct);
            var stream = tcp.GetStream();
            var bytes = Encoding.ASCII.GetBytes(payload);
            await stream.WriteAsync(bytes, ct);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "text-send: TCP send to {Host}:{Port} failed", host, port);
        }
    }

    private async Task SendSerialAsync(
        IReadOnlyDictionary<string, ConfigValue> parameters,
        EventEnvelope trigger,
        IExpressionEvaluator evaluator,
        string payload,
        CancellationToken ct)
    {
        var portName = await ActionParameterResolver.ResolveRequiredAsync(
            "serial-port", parameters, trigger, evaluator, ct);

        var baudrateStr = await ActionParameterResolver.ResolveOptionalAsync(
            "baudrate", parameters, trigger, evaluator, ct)
            ?? "9600";

        if (!int.TryParse(baudrateStr, out var baudrate))
        {
            _logger.LogError("text-send: invalid baudrate '{Baudrate}'", baudrateStr);
            return;
        }

        _logger.LogDebug("text-send: Serial {Port}@{Baudrate} <- {Command}",
            portName, baudrate, payload.TrimEnd());

        try
        {
            using var serial = new SerialPort(portName, baudrate)
            {
                WriteTimeout = 5000,
                Encoding = Encoding.ASCII,
            };
            serial.Open();
            serial.Write(payload);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "text-send: serial send to {Port} failed", portName);
        }
    }

    private async Task SendFileAsync(
        IReadOnlyDictionary<string, ConfigValue> parameters,
        EventEnvelope trigger,
        IExpressionEvaluator evaluator,
        string payload,
        CancellationToken ct)
    {
        var path = await ActionParameterResolver.ResolveRequiredAsync(
            "path", parameters, trigger, evaluator, ct);

        _logger.LogDebug("text-send: File {Path} <- {Content}", path, payload.TrimEnd());

        try
        {
            await File.WriteAllTextAsync(path, payload, Encoding.ASCII, ct);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "text-send: file write to {Path} failed", path);
        }
    }

    private string ResolveLineTerminator(string name) => name.ToLowerInvariant() switch
    {
        "lf" => "\n",
        "cr" => "\r",
        "crlf" => "\r\n",
        "none" => "",
        _ => throw new ArgumentException(
            $"text-send: unknown line-terminator '{name}'. Use cr, lf, crlf, or none."),
    };
}