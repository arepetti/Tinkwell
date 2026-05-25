using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using Google.Protobuf;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tw;

namespace Tinkwell.Cli.Commands.Coap;

public sealed class CoapServerSettings : TwSettings
{
    [Description("UDP port to listen on")]
    [CommandOption("--port")]
    [DefaultValue(5684)]
    public int Port { get; set; } = 5684;

    [Description("Bind address")]
    [CommandOption("--bind")]
    [DefaultValue("0.0.0.0")]
    public string Bind { get; set; } = "0.0.0.0";

    [Description("Fixed response: /path=body (repeatable)")]
    [CommandOption("--path")]
    public string[]? Paths { get; set; }

    [Description("Heartbeat mailbox path (e.g. /hub/heartbeat)")]
    [CommandOption("--mailbox")]
    public string? Mailbox { get; set; }

    [Description("Pre-queue command[:json] for hub-push dispatch (repeatable)")]
    [CommandOption("--queue")]
    public string[]? Queue { get; set; }

    [Description("CoAP path prefix for command dispatch")]
    [CommandOption("--prefix")]
    [DefaultValue("tw")]
    public string Prefix { get; set; } = "tw";

    [Description("Log incoming request payloads")]
    [CommandOption("--log-payload")]
    [DefaultValue(false)]
    public bool LogPayload { get; set; }
}

/// <summary>
/// Command entry for a queued hub-push command.
/// Command is the endpoint name (e.g. "reboot", "set-config").
/// Payload is the protobuf-encoded bytes, or null for empty-body commands.
/// </summary>
internal record CommandEntry(string Command, byte[]? Payload);

[CliCommand("coap", "server", Description = "Start a CoAP server with hub-push mailbox (Ctrl+C to stop)")]
public sealed class CoapServerCommand : AsyncCommand<CoapServerSettings>
{
    private readonly ConcurrentQueue<CommandEntry> _commandQueue = new();
    private long _heartbeatCount;
    private long _commandsDispatched;

    /// <summary>
    /// Maps command names to their protobuf message type for JSON &lt;-&gt; binary transcoding.
    /// </summary>
    private static readonly Dictionary<string, MessageParser> CommandParsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["set-config"]    = SetConfigCmd.Parser,
        ["ota-available"] = OtaAvailableCmd.Parser,
        ["app"]           = AppCmd.Parser,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, CoapServerSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            if (settings.Queue is not null)
            {
                foreach (var raw in settings.Queue)
                    _commandQueue.Enqueue(ParseQueueEntry(raw));
            }

            var options = new CoapServerOptions { Port = settings.Port };
            var server = new CoapServer(options);

            bool hasRoutes = false;

            if (settings.Paths is not null)
            {
                foreach (var entry in settings.Paths)
                {
                    var (pattern, body) = ParsePathEntry(entry);
                    var responseBody = body is not null
                        ? Encoding.UTF8.GetBytes(body.Replace("\\n", "\n"))
                        : [];
                    CoapContentFormat? responseFormat = body is not null
                        ? CoapContentFormat.TextPlain
                        : null;

                    server.Map(pattern, new FixedHandler(responseBody, responseFormat,
                        req => LogRequest(output, settings, req)));
                    hasRoutes = true;
                }
            }

            if (settings.Mailbox is not null)
            {
                server.Map(settings.Mailbox, new MailboxHandler(
                    _commandQueue,
                    settings.Prefix,
                    () => Interlocked.Increment(ref _heartbeatCount),
                    count => Interlocked.Add(ref _commandsDispatched, count),
                    req => LogRequest(output, settings, req),
                    (payload) => LogHeartbeat(output, payload)));
                hasRoutes = true;

                // Register /hub/telemetry handler for sensor data logging.
                server.Map("/hub/telemetry", new TelemetryHandler(
                    req => LogRequest(output, settings, req),
                    payload => LogTelemetry(output, payload)));
            }

            if (!hasRoutes)
            {
                server.Map("/#", new EchoHandler(
                    req => LogRequest(output, settings, req)));
            }

            output.WriteMarkup(
                $"CoAP server listening on [bold]{settings.Bind}:{settings.Port}[/] [dim](Ctrl+C to stop)[/]");

            if (settings.Mailbox is not null)
            {
                output.WriteMarkup(
                    $"  Mailbox: [cyan]{Markup.Escape(settings.Mailbox)}[/]" +
                    $" [dim]({_commandQueue.Count} commands queued, prefix=/{settings.Prefix}/)[/]");

                if (!settings.NonInteractive)
                    output.WriteMarkup("[dim]Type commands: command[:json] (e.g. reboot, set-config:{\"entries\":[...]})[/]");
            }

            var serverTask = server.RunAsync(ct);
            var stdinTask = settings.Mailbox is not null && !settings.NonInteractive
                ? ReadStdinAsync(ct)
                : Task.CompletedTask;

            try
            {
                var completed = await Task.WhenAny(serverTask, stdinTask);
                /* M10 fix: propagate faults from the first completed task. */
                if (completed.IsFaulted)
                    await completed;
            }
            catch (OperationCanceledException)
            {
            }

            var hb = Interlocked.Read(ref _heartbeatCount);
            var dispatched = Interlocked.Read(ref _commandsDispatched);
            output.WriteMarkup(
                $"\n[dim]{hb} heartbeat(s) received, {dispatched} command(s) dispatched[/]");

            return 0;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Parses a queue entry in the format "command" or "command:json".
    /// JSON is auto-encoded to protobuf if the command is known.
    /// </summary>
    private static CommandEntry ParseQueueEntry(string raw)
    {
        var colonIdx = raw.IndexOf(':');
        if (colonIdx < 0)
            return new CommandEntry(raw.Trim(), null);

        var command = raw[..colonIdx].Trim();
        var jsonStr = raw[(colonIdx + 1)..].Trim();

        if (string.IsNullOrEmpty(jsonStr))
            return new CommandEntry(command, null);

        if (CommandParsers.TryGetValue(command, out var parser))
        {
            var msg = parser.ParseJson(jsonStr);
            return new CommandEntry(command, msg.ToByteArray());
        }

        return new CommandEntry(command, Encoding.UTF8.GetBytes(jsonStr));
    }

    private static (string Pattern, string? Body) ParsePathEntry(string entry)
    {
        if (!entry.StartsWith('/'))
            entry = "/" + entry;

        var eqIndex = entry.IndexOf('=', 1);
        if (eqIndex < 0)
            return (entry, null);

        return (entry[..eqIndex], entry[(eqIndex + 1)..]);
    }

    private static void LogRequest(OutputContext output, CoapServerSettings settings, CoapRequest req)
    {
        var method = CoapCode.ToMethodString((byte)req.Method);
        output.WriteMarkup(
            $"[green]{method}[/] {Markup.Escape(req.Path)} [dim]from {req.RemoteEndpoint}[/]");

        if (settings.LogPayload && req.Payload.Length > 0)
        {
            try
            {
                var heartbeat = HeartbeatPayload.Parser.ParseFrom(req.Payload.Span);
                output.WriteMarkup($"  [dim]Decoded HeartbeatPayload:[/] {JsonFormatter.Default.Format(heartbeat)}");
            }
            catch
            {
                var text = Encoding.UTF8.GetString(req.Payload.Span);
                foreach (var line in text.Split('\n'))
                    output.WriteMarkup($"  [dim]{Markup.Escape(line)}[/]");
            }
        }
    }

    private static void LogHeartbeat(OutputContext output, ReadOnlyMemory<byte> payload)
    {
        try
        {
            var msg = HeartbeatPayload.Parser.ParseFrom(payload.Span);
            var json = JsonFormatter.Default.Format(msg);
            output.WriteMarkup($"  [cyan]heartbeat:[/] {Markup.Escape(json)}");
        }
        catch
        {
            output.WriteMarkup($"  [dim]heartbeat: {payload.Length} bytes (raw)[/]");
        }
    }

    private static void LogTelemetry(OutputContext output, ReadOnlyMemory<byte> payload)
    {
        try
        {
            var msg = TelemetryPush.Parser.ParseFrom(payload.Span);
            foreach (var r in msg.Readings)
                output.WriteMarkup($"  [yellow]sensor[/] {Markup.Escape(r.Name)}={r.Value:F2}");
        }
        catch
        {
            output.WriteMarkup($"  [dim]telemetry: {payload.Length} bytes (raw)[/]");
        }
    }

    private async Task ReadStdinAsync(CancellationToken ct)
    {
        try
        {
            await Task.Yield();
            while (!ct.IsCancellationRequested)
            {
                var line = await Task.Run(() => Console.ReadLine(), ct);
                if (line is null)
                    break;
                if (line.Trim().Length > 0)
                    _commandQueue.Enqueue(ParseQueueEntry(line.Trim()));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class FixedHandler(
        byte[] body, CoapContentFormat? contentFormat, Action<CoapRequest> log) : ICoapRequestHandler
    {
        public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
        {
            log(request);
            return Task.FromResult(new CoapResponse
            {
                Code = CoapCode.Content,
                Payload = body.Length > 0 ? body : null,
                ContentFormat = contentFormat,
            });
        }
    }

    private sealed class EchoHandler(Action<CoapRequest> log) : ICoapRequestHandler
    {
        public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
        {
            log(request);
            var body = $"{CoapCode.ToMethodString((byte)request.Method)} {request.Path}\n";
            return Task.FromResult(CoapResponse.Content(
                Encoding.UTF8.GetBytes(body), CoapContentFormat.TextPlain));
        }
    }

    /// <summary>
    /// Handles heartbeat: responds with HeartbeatReply {pending: N},
    /// then sends each queued command as an individual CoAP POST to the device.
    /// </summary>
    private sealed class MailboxHandler(
        ConcurrentQueue<CommandEntry> queue,
        string prefix,
        Func<long> incrementHeartbeat,
        Action<long> addDispatched,
        Action<CoapRequest> log,
        Action<ReadOnlyMemory<byte>> logHeartbeat) : ICoapRequestHandler
    {
        public async Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
        {
            log(request);
            incrementHeartbeat();

            if (request.Payload.Length > 0)
                logHeartbeat(request.Payload);

            int pending = queue.Count;

            // Respond with HeartbeatReply protobuf (pending count only).
            var reply = new HeartbeatReply { Pending = (uint)pending };
            var replyBytes = reply.ToByteArray();

            // Fire-and-forget: push queued commands to the device after responding.
            if (pending > 0 && request.RemoteEndpoint is not null)
            {
                _ = Task.Run(async () =>
                {
                    long dispatched = 0;
                    var deviceHost = request.RemoteEndpoint.Address.ToString();
                    var devicePort = 5683;

                    while (queue.TryDequeue(out var entry))
                    {
                        var path = $"/{prefix}/{entry.Command}";
                        try
                        {
                            var coapRequest = new CoapClientRequest(
                                entry.Payload ?? [],
                                CoapContentFormat.ApplicationOctetStream)
                            {
                                Method = CoapMethod.Post,
                            };
                            await CoapClient.SendAsync(
                                deviceHost, devicePort, path,
                                query: null,
                                coapRequest,
                                new CoapClientRequestOptions(),
                                CancellationToken.None);
                            dispatched++;
                        }
                        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                        catch (Exception ex)
                        {
                            System.Console.Error.WriteLine(
                                $"[hub-push] POST {path} failed: {ex.Message}");
                        }
                    }

                    addDispatched(dispatched);
                }, CancellationToken.None);
            }

            return new CoapResponse
            {
                Code = CoapCode.Content,
                Payload = replyBytes,
                ContentFormat = CoapContentFormat.ApplicationOctetStream,
            };
        }
    }

    /// <summary>
    /// Handles POST /hub/telemetry: decodes TelemetryPush and responds with TelemetryReply.
    /// </summary>
    private sealed class TelemetryHandler(
        Action<CoapRequest> log,
        Action<ReadOnlyMemory<byte>> logTelemetry) : ICoapRequestHandler
    {
        public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
        {
            log(request);

            if (request.Payload.Length > 0)
                logTelemetry(request.Payload);

            var reply = new TelemetryReply { NextIntervalS = 0 };
            var replyBytes = reply.ToByteArray();

            return Task.FromResult(new CoapResponse
            {
                Code = CoapCode.Content,
                Payload = replyBytes,
                ContentFormat = CoapContentFormat.ApplicationOctetStream,
            });
        }
    }
}