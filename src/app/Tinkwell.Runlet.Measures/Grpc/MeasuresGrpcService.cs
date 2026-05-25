using System.Globalization;
using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Measures;

namespace Tinkwell.Runlet.Measures.Grpc.V1;

internal sealed class MeasuresGrpcService : Measures.MeasuresBase
{
    private readonly MeasureRegistryHolder _holder;
    private readonly ILogger<MeasuresGrpcService> _logger;

    public MeasuresGrpcService(MeasureRegistryHolder holder, ILogger<MeasuresGrpcService> logger)
    {
        _holder = holder;
        _logger = logger;
    }

    public override async Task<RegisterMeasureResponse> Register(
        RegisterMeasureRequest request, ServerCallContext context)
    {
        var registry = GetRegistry();
        var def = ToDomain(request.Definition);
        var meta = ToDomain(request.Metadata);

        MeasureValue? initialValue = null;
        if (request.InitialValue is { } iv && iv.Type is not (null or "" or "Undefined"))
            initialValue = ToDomainValue(def, iv);

        await registry.RegisterAsync(def, meta, initialValue, context.CancellationToken);
        return new RegisterMeasureResponse();
    }

    public override async Task<UpdateMeasureResponse> Update(
        UpdateMeasureRequest request, ServerCallContext context)
    {
        var registry = GetRegistry();

        var def = await registry.FindDefinitionAsync(request.Name, context.CancellationToken)
            ?? throw new RpcException(new Status(
                StatusCode.NotFound, $"Measure '{request.Name}' not found."));

        var value = ToDomainValue(def, request.Value);
        await registry.UpdateAsync(request.Name, value, ct: context.CancellationToken);
        return new UpdateMeasureResponse();
    }

    public override async Task<GetMeasureResponse> Get(
        GetMeasureRequest request, ServerCallContext context)
    {
        var registry = GetRegistry();
        var measure = await registry.FindAsync(request.Name, context.CancellationToken);

        if (measure is null)
            return new GetMeasureResponse { Found = false };

        return new GetMeasureResponse
        {
            Found = true,
            Measure = ToProto(measure)
        };
    }

    public override async Task<ListMeasuresResponse> List(
        ListMeasuresRequest request, ServerCallContext context)
    {
        var registry = GetRegistry();
        var measures = await registry.FindAllAsync(context.CancellationToken);

        var response = new ListMeasuresResponse();
        foreach (var m in measures)
            response.Measures.Add(ToProto(m));

        return response;
    }

    public override async Task<GetDefinitionResponse> GetDefinition(
        GetDefinitionRequest request, ServerCallContext context)
    {
        var registry = GetRegistry();
        var def = await registry.FindDefinitionAsync(request.Name, context.CancellationToken);

        if (def is null)
            return new GetDefinitionResponse { Found = false };

        return new GetDefinitionResponse
        {
            Found = true,
            Definition = ToProto(def)
        };
    }

    public override async Task Watch(
        WatchMeasuresRequest request,
        IServerStreamWriter<MeasureEvent> responseStream,
        ServerCallContext context)
    {
        var registry = GetRegistry();

        var channel = Channel.CreateBounded<ValueChangedEventArgs>(
            new BoundedChannelOptions(WatchChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
        var dropTracker = new ChannelDropTracker("measures.grpc.watch", _logger);

        // Just subscribe to ValueChanged and forward events. The watch loop
        // itself is driven exactly once, process-wide, by MeasureWatchWorker.
        // Calling registry.WatchAsync(...) here used to spin up an extra
        // store gRPC stream per gRPC client, which made every actual store
        // mutation fire ValueChanged once per active loop — every connected
        // client received N copies of every event (with N = number of
        // concurrent gRPC Watch consumers + the worker).
        registry.ValueChanged += OnValueChanged;
        try
        {
            await DrainAsync(channel.Reader, responseStream, context.CancellationToken);
        }
        finally
        {
            registry.ValueChanged -= OnValueChanged;
            channel.Writer.TryComplete();
        }

        void OnValueChanged(object? sender, ValueChangedEventArgs e) =>
            dropTracker.TryWrite(channel.Writer, e);
    }

    private const int WatchChannelCapacity = 256;

    private async Task DrainAsync(
        ChannelReader<ValueChangedEventArgs> reader,
        IServerStreamWriter<MeasureEvent> responseStream,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var e in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await responseStream.WriteAsync(new MeasureEvent
                    {
                        Name = e.Name,
                        OldValue = ToProto(e.OldValue),
                        NewValue = ToProto(e.NewValue),
                    }, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Failed to write to measures gRPC response stream");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private IMeasureRegistry GetRegistry() =>
        _holder.Registry
        ?? throw new RpcException(new Status(
            StatusCode.Unavailable, "Measure registry is not yet initialized."));

    private static MeasureDefinition ToDomain(MeasureDefinitionProto proto)
    {
        var def = new MeasureDefinition
        {
            Name = proto.Name,
            Type = Enum.TryParse<MeasureType>(proto.Type, true, out var t)
                ? t : MeasureType.Number,
            Attributes = ParseAttributes(proto.Attributes),
        };

        if (!string.IsNullOrEmpty(proto.QuantityType))
            def.QuantityType = proto.QuantityType;
        if (!string.IsNullOrEmpty(proto.Unit))
            def.Unit = proto.Unit;
        if (proto.HasMinimum)
            def.Minimum = proto.Minimum;
        if (proto.HasMaximum)
            def.Maximum = proto.Maximum;
        if (proto.HasPrecision)
            def.Precision = proto.Precision;
        if (proto.HasTtlSeconds)
            def.Ttl = TimeSpan.FromSeconds(proto.TtlSeconds);

        return def;
    }

    private static MeasureMetadata ToDomain(MeasureMetadataProto? proto)
    {
        if (proto is null)
            return new MeasureMetadata();

        return new MeasureMetadata
        {
            Description = string.IsNullOrEmpty(proto.Description) ? null : proto.Description,
            Category = string.IsNullOrEmpty(proto.Category) ? null : proto.Category,
            Tags = proto.Tags.Count > 0 ? proto.Tags.ToList() : [],
        };
    }

    private static MeasureValue ToDomainValue(MeasureDefinition def, MeasureValueProto? proto)
    {
        if (proto is null || proto.Type == "Undefined")
            return MeasureValue.Undefined;

        if (proto.Type == "Number")
            return MeasureValue.FromValue(def, proto.NumericValue, DateTime.UtcNow);

        if (proto.Type == "String")
            return MeasureValue.FromValue(def, proto.StringValue, DateTime.UtcNow);

        return MeasureValue.Undefined;
    }

    private static MeasureProto ToProto(Measure measure)
    {
        return new MeasureProto
        {
            Definition = ToProto(measure.Definition),
            Metadata = ToProto(measure.Metadata),
            Value = ToProto(measure.Value),
        };
    }

    private static MeasureDefinitionProto ToProto(MeasureDefinition def)
    {
        var proto = new MeasureDefinitionProto
        {
            Name = def.Name,
            Type = def.Type.ToString(),
            Attributes = def.Attributes.ToString(),
            QuantityType = def.QuantityType,
        };

        if (def.Unit is not null)
            proto.Unit = def.Unit;
        if (def.Minimum is double min)
            proto.Minimum = min;
        if (def.Maximum is double max)
            proto.Maximum = max;
        if (def.Precision is int prec)
            proto.Precision = prec;
        if (def.Ttl is TimeSpan ttl)
            proto.TtlSeconds = (int)ttl.TotalSeconds;

        return proto;
    }

    private static MeasureMetadataProto ToProto(MeasureMetadata meta)
    {
        var proto = new MeasureMetadataProto
        {
            Description = meta.Description ?? "",
            Category = meta.Category ?? "",
        };

        foreach (var tag in meta.Tags)
            proto.Tags.Add(tag);

        return proto;
    }

    private static MeasureValueProto ToProto(MeasureValue value)
    {
        return value.Type switch
        {
            MeasureValueType.Number => new MeasureValueProto
            {
                Type = "Number",
                NumericValue = value.AsDouble(),
                Unit = value.AsQuantity().Unit.ToString() ?? "",
            },
            MeasureValueType.String => new MeasureValueProto
            {
                Type = "String",
                StringValue = value.AsString(),
            },
            _ => new MeasureValueProto { Type = "Undefined" },
        };
    }

    private static MeasureValueProto ToProto(MeasureValue? value)
    {
        if (value is null)
            return new MeasureValueProto { Type = "Undefined" };
        return ToProto(value.Value);
    }

    private static MeasureAttributes ParseAttributes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return MeasureAttributes.None;

        if (Enum.TryParse<MeasureAttributes>(raw, true, out var a))
            return a;

        return MeasureAttributes.None;
    }
}
