using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.Signals.Configuration;

namespace Tinkwell.Runlet.Signals.Grpc.V1;

internal sealed class SignalsGrpcService : Signals.SignalsBase
{
    private readonly SignalRegistry _registry;
    private readonly ILogger<SignalsGrpcService> _logger;

    public SignalsGrpcService(SignalRegistry registry, ILogger<SignalsGrpcService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public override Task<CreateSignalResponse> Create(
        CreateSignalRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Signal name is required."));

        if (string.IsNullOrWhiteSpace(request.WhenExpression))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "when expression is required."));

        SignalDuration? duration = null;
        if (!string.IsNullOrWhiteSpace(request.ForDuration))
            duration = ParseDuration(request.ForDuration);

        var properties = new Dictionary<string, string>(request.Properties, StringComparer.Ordinal);

        var definition = new SignalDefinition(
            Name: request.Name,
            WhenExpression: request.WhenExpression,
            UntilExpression: string.IsNullOrWhiteSpace(request.UntilExpression)
                ? null : request.UntilExpression,
            Duration: duration,
            ParentMeasure: null,
            Properties: properties,
            Location: new SourceLocation("grpc", 0, 0));

        _registry.Register(definition);
        return Task.FromResult(new CreateSignalResponse());
    }

    public override Task<ListSignalsResponse> List(
        ListSignalsRequest request, ServerCallContext context)
    {
        var all = _registry.ListAll();
        var response = new ListSignalsResponse();

        foreach (var def in all)
            response.Signals.Add(ToProto(def));

        return Task.FromResult(response);
    }

    public override async Task Watch(
        WatchSignalsRequest request,
        IServerStreamWriter<SignalEvent> responseStream,
        ServerCallContext context)
    {
        var channel = Channel.CreateBounded<SignalFiredEventArgs>(
            new BoundedChannelOptions(WatchChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
        var dropTracker = new ChannelDropTracker("signals.grpc.watch", _logger);

        var tcs = new TaskCompletionSource();
        using var reg = context.CancellationToken.Register(() => tcs.TrySetResult());

        _registry.SignalFired += OnFired;
        var drain = DrainAsync(channel.Reader, responseStream, context.CancellationToken);
        try
        {
            await tcs.Task;
        }
        finally
        {
            _registry.SignalFired -= OnFired;
            channel.Writer.TryComplete();
            try { await drain; }
            catch (OperationCanceledException)
            {
            }
        }

        void OnFired(object? sender, SignalFiredEventArgs e) =>
            dropTracker.TryWrite(channel.Writer, e);
    }

    private const int WatchChannelCapacity = 256;

    private async Task DrainAsync(
        ChannelReader<SignalFiredEventArgs> reader,
        IServerStreamWriter<SignalEvent> responseStream,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var e in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var evt = new SignalEvent
                    {
                        Name = e.SignalName,
                        Timestamp = Timestamp.FromDateTime(
                            DateTime.SpecifyKind(e.Timestamp, DateTimeKind.Utc)),
                    };

                    foreach (var (k, v) in e.Properties)
                        evt.Properties[k] = v;

                    await responseStream.WriteAsync(evt, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Failed to write to signals gRPC response stream");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static SignalDefinitionProto ToProto(SignalDefinition def)
    {
        var proto = new SignalDefinitionProto
        {
            Name = def.Name,
            WhenExpression = def.WhenExpression,
        };

        if (def.UntilExpression is not null)
            proto.UntilExpression = def.UntilExpression;

        if (def.Duration is not null)
            proto.ForDuration = FormatDuration(def.Duration);

        if (def.ParentMeasure is not null)
            proto.ParentMeasure = def.ParentMeasure;

        foreach (var (k, v) in def.Properties)
            proto.Properties[k] = v;

        return proto;
    }

    private static string FormatDuration(SignalDuration duration) => duration switch
    {
        SignalDuration.Seconds s => $"{s.Value}",
        SignalDuration.Parsed p => p.Text,
        SignalDuration.Expression e => e.Text,
        _ => ""
    };

    private static SignalDuration ParseDuration(string raw)
    {
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return new SignalDuration.Seconds(seconds);

        return new SignalDuration.Parsed(raw);
    }
}
