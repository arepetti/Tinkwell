using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Measures;
using Tinkwell.Services;
using UnitsNet;

namespace Tinkwell.Store.Services;

public class StoreService : Tinkwell.Services.Store.StoreBase
{
    public StoreService(ILogger<StoreService> logger, IRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public override async Task<Empty> Register(StoreRegisterRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            var definition = ToMeasureDefinition(request.Definition);
            var metadata = ToMeasureMetadata(request.Metadata);
            await _registry.RegisterAsync(definition, metadata, context.CancellationToken);
        });
    }

    public override async Task<Empty> RegisterMany(StoreRegisterManyRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            var measures = request.Items.Select(item =>
                (ToMeasureDefinition(item.Definition), ToMeasureMetadata(item.Metadata)));
            await _registry.RegisterManyAsync(measures, context.CancellationToken);
        });
    }

    public override async Task<Empty> Update(StoreUpdateRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            var value = ToMeasureValue(request.Value);
            await _registry.UpdateAsync(request.Name, value, context.CancellationToken);
        });
    }

    public override async Task<Empty> UpdateMany(StoreUpdateManyRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            var measures = request.Items.Select(item =>
                (item.Name, ToMeasureValue(item.Value)));
            await _registry.UpdateManyAsync(measures, context.CancellationToken);
        });
    }

    public override async Task<Empty> SetMeasureValue(SetMeasureValueRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            var measure = _registry.Find(request.Name);
            if (measure is null)
                throw new KeyNotFoundException($"Measure with name '{request.Name}' not found.");

            var currentType = measure.Value.Type;
            var measureType = measure.Definition.Type;

            if (measureType == MeasureType.Number || (measureType == MeasureType.Dynamic && currentType == MeasureValueType.Number))
            {
                var value = Quant.ParseAndConvert(
                    measure.Definition.QuantityType,
                    measure.Definition.Unit,
                    request.ValueString
                );
                await _registry.UpdateAsync(
                    request.Name,
                    new MeasureValue(value),
                    context.CancellationToken
                );
            }
            else if (measureType == MeasureType.String || (measureType == MeasureType.Dynamic && currentType == MeasureValueType.String))
            {
                await _registry.UpdateAsync(
                    request.Name,
                    new MeasureValue(request.ValueString),
                    context.CancellationToken
                );
            }
            else
            {
                // We can't reliably determine the appropriate type of an empty dynamic measure.
                throw new ArgumentException("Cannot set the value of a dynamic measure without a defined type.");
            }
        });
    }

    public override async Task<StoreMeasure> Find(StoreFindRequest request, ServerCallContext context)
    {
        return await RunWithErrorHandling(async () =>
        {
            return ToStoreMeasure(await _registry.FindAsync(request.Name, context.CancellationToken));
        });
    }

    public override async Task FindAll(StoreFindAllRequest request, IServerStreamWriter<StoreMeasure> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            IEnumerable<Measure> measures;
            if (request.Names.Any())
                measures = await _registry.FindAllAsync(request.Names, context.CancellationToken);
            else
                measures = await _registry.FindAllAsync(context.CancellationToken);

            foreach (var measure in measures)
                await responseStream.WriteAsync(ToStoreMeasure(measure));
        });
    }

    public override async Task FindAllDefinitions(StoreFindAllDefinitionsRequest request, IServerStreamWriter<StoreDefinition> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            IEnumerable<MeasureDefinition> definitions;
            if (request.Names.Any())
                definitions = await _registry.FindAllDefinitionsAsync(request.Names, context.CancellationToken);
            else
                definitions = await _registry.FindAllDefinitionsAsync(context.CancellationToken);

            foreach (var definition in definitions)
                await responseStream.WriteAsync(ToStoreDefinition(definition));
        });
    }

    public override async Task Search(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            var allMeasures = await _registry.FindAllAsync(context.CancellationToken);

            var regex = TextHelpers.PatternToRegex(request.Query);
            var filteredMeasures = allMeasures.Where(m =>
            {
                bool matchesQuery = string.IsNullOrEmpty(request.Query) || regex.IsMatch(m.Name);

                bool matchesTags = !request.Tags.Any() ||
                                   request.Tags.All(tag => m.Metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

                bool matchesCategory = string.IsNullOrEmpty(request.Category) ||
                                       (m.Metadata.Category != null && m.Metadata.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase));

                return matchesQuery && matchesTags && matchesCategory;
            });

            foreach (var measure in filteredMeasures)
            {
                if (request.IncludeValues)
                    await responseStream.WriteAsync(new SearchResponse { Measure = ToStoreMeasure(measure) });
                else
                    await responseStream.WriteAsync(new SearchResponse { Info = ToStoreMeasureInfo(measure) });
            }
        });
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<StoreValueChange> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            await HandleSubscription(
                responseStream, context, name => name.Equals(request.Name, StringComparison.Ordinal));
        });
    }

    public override async Task SubscribeMany(SubscribeManyRequest request, IServerStreamWriter<StoreValueChange> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            var names = new HashSet<string>(request.Names);
            await HandleSubscription(responseStream, context, names.Contains);
        });
    }

    public override async Task SubscribeManyMatching(SubscribeManyMatchingRequest request, IServerStreamWriter<StoreValueChange> responseStream, ServerCallContext context)
    {
        await RunWithErrorHandling(async () =>
        {
            var regex = TextHelpers.PatternToRegex(request.Pattern);
            await HandleSubscription(responseStream, context, regex.IsMatch);
        });
    }

    private readonly ILogger<StoreService> _logger;
    private readonly IRegistry _registry;

    private async Task HandleSubscription(
        IServerStreamWriter<StoreValueChange> responseStream,
        ServerCallContext context,
        Func<string, bool> nameMatcher)
    {
        var tcs = new TaskCompletionSource();

        EventHandler<ValueChangedEventArgs> valueChangedHandler = async (_, args) =>
        {
            if (!nameMatcher(args.Name))
                return;

            try
            {
                await responseStream.WriteAsync(ToStoreValueChange(args));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send update for subscription. Client may have disconnected.");
                tcs.TrySetCanceled();
            }
        };

        _registry.ValueChanged += valueChangedHandler;
        try
        {
            using var _ = context.CancellationToken.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
        }
        finally
        {
            _registry.ValueChanged -= valueChangedHandler;
        }
    }

    private Task<Empty> RunWithErrorHandling(Func<Task> action)
    {
        return RunWithErrorHandling(async () =>
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }

            return new Empty();
        });
    }

    private async Task<TResult> RunWithErrorHandling<TResult>(Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentNullException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new RpcException(new Status(StatusCode.OutOfRange, ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (NotSupportedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    #region From protobuf to C#

    private MeasureDefinition ToMeasureDefinition(StoreDefinition storeDefinition)
    {
        return new MeasureDefinition
        {
            Name = storeDefinition.Name,
            Type = (MeasureType)storeDefinition.Type,
            Attributes = (MeasureAttributes)storeDefinition.Attributes,
            QuantityType = storeDefinition.QuantityType,
            Unit = storeDefinition.Unit,
        };
    }

    private MeasureMetadata ToMeasureMetadata(StoreMetadataInput storeMetadataInput)
    {
        return new MeasureMetadata(DateTime.UtcNow)
        {
            Tags = storeMetadataInput.Tags.ToList(),
            Category = storeMetadataInput.HasCategory ? storeMetadataInput.Category : null,
            Description = storeMetadataInput.HasDescription ? storeMetadataInput.Description : null,
        };
    }

    private MeasureValue ToMeasureValue(StoreValue storeValue)
    {
        if (storeValue.PayloadCase == StoreValue.PayloadOneofCase.NumberValue)
            return new MeasureValue(Scalar.FromAmount(storeValue.NumberValue), storeValue.Timestamp.ToDateTime());
        
        if (storeValue.PayloadCase == StoreValue.PayloadOneofCase.StringValue)
            return new MeasureValue(storeValue.StringValue, storeValue.Timestamp.ToDateTime());
        
        return MeasureValue.Undefined;
    }

    #endregion

    #region From C# to protobuf

    private StoreMeasure ToStoreMeasure(Measure measure)
    {
        return new StoreMeasure
        {
            Definition = ToStoreDefinition(measure.Definition),
            Metadata = ToStoreMetadata(measure.Metadata),
            Value = ToStoreValue(measure.Value)
        };
    }

    private StoreMeasureInfo ToStoreMeasureInfo(Measure measure)
    {
        return new StoreMeasureInfo
        {
            Definition = ToStoreDefinition(measure.Definition),
            Metadata = ToStoreMetadata(measure.Metadata)
        };
    }

    private StoreDefinition ToStoreDefinition(MeasureDefinition definition)
    {
        var storeDefinition = new StoreDefinition
        {
            Name = definition.Name,
            Type = (StoreDefinition.Types.Type)definition.Type,
            Attributes = (int)definition.Attributes,
            QuantityType = definition.QuantityType,
            Unit = definition.Unit,
        };

        if (definition.Ttl.HasValue)
            storeDefinition.Ttl = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(definition.Ttl.Value);

        if (definition.Minimum.HasValue)
            storeDefinition.Minimum = definition.Minimum.Value;

        if (definition.Maximum.HasValue)
            storeDefinition.Maximum = definition.Maximum.Value;

        if (definition.Precision.HasValue)
            storeDefinition.Precision = definition.Precision.Value;

        return storeDefinition;
    }

    private StoreMetadata ToStoreMetadata(MeasureMetadata metadata)
    {
        var storeMetadata = new StoreMetadata
        {
            CreatedAt = Timestamp.FromDateTime(metadata.CreatedAt.ToUniversalTime()),
        };
        storeMetadata.Tags.AddRange(metadata.Tags);

        if (metadata.Category is not null)
            storeMetadata.Category = metadata.Category;

        if (metadata.Description is not null)
            storeMetadata.Description = metadata.Description;

        return storeMetadata;
    }

    private StoreValue ToStoreValue(MeasureValue value)
    {
        var storeValue = new StoreValue
        {
            Timestamp = Timestamp.FromDateTime(value.Timestamp.ToUniversalTime())
        };

        if (value.Type == MeasureValueType.Number)
            storeValue.NumberValue = (double)value;
        else if (value.Type == MeasureValueType.String)
            storeValue.StringValue = (string)value;

        return storeValue;
    } 

    private StoreValueChange ToStoreValueChange(ValueChangedEventArgs args)
    {
        var change = new StoreValueChange
        {
            Name = args.Name,
            NewValue = ToStoreValue(args.NewValue)
        };

        if (args.OldValue.HasValue)
            change.OldValue = ToStoreValue(args.OldValue.Value);

        return change;
    }

    #endregion
}