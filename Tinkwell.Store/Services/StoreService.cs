using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Services;
using UnitsNet;

namespace Tinkwell.Store.Services;

public sealed class StoreService : Tinkwell.Services.Store.StoreBase
{
    public StoreService(ILogger<StoreService> logger, IRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public override Task<StoreListReply> List(StoreListRequest request, ServerCallContext context)
    {
        return RunWithErrorHandling(() =>
        {
            var result = new StoreListReply();
            var measures = _registry.FindAll();

            if (!string.IsNullOrEmpty(request.Query))
            {
                try
                {
                    var regex = new Regex(Tinkwell.Bootstrapper.Expressions.TextHelpers.GitLikeWildcardToRegex(request.Query), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    measures = measures.Where(x => regex.IsMatch(x.Name));
                }
                catch (ArgumentException e)
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid query pattern: {e.Message}"));
                }
            }

            if (!string.IsNullOrEmpty(request.Tag))
            {
                measures = measures.Where(x => x.Tags.Contains(request.Tag, StringComparer.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(request.Category))
            {
                measures = measures.Where(x => string.Equals(x.Category, request.Category, StringComparison.OrdinalIgnoreCase));
            }

            result.Items.AddRange(measures.Select(x =>
            {
                var item = new StoreListReply.Types.Item()
                {
                    Name = x.Name,
                    QuantityType = x.QuantityType,
                    Unit = x.Unit,
                    Minimum = x.Minimum,
                    Maximum = x.Maximum,
                    Category = x.Category ?? string.Empty,
                    Precision = x.Precision,
                };
                item.Tags.AddRange(x.Tags);

                if (request.IncludeValues)
                {
                    var value = _registry.GetCurrentValue(x.Name);
                    if (value is not null)
                        item.Value = value.ToString("G", CultureInfo.InvariantCulture);
                }
                    
                return item;
            }));

            return result;
        });
    }

    public override Task<StoreRegisterReply> Register(StoreRegisterRequest request, ServerCallContext context)
    {
        return RunWithErrorHandling(() =>
        {
            _registry.Register(new()
            {
                Name = request.Name,
                Ttl = request.Ttl?.ToTimeSpan(),
                QuantityType = request.QuantityType,
                Unit = request.Unit,
                Minimum = request.Minimum,
                Maximum = request.Maximum,
                Category = request.Category,
                Precision = request.Precision,
            });
            _registry.Find(request.Name).Tags.AddRange(request.Tags); // Add tags after registration

            return new StoreRegisterReply();
        });
    }

    public override Task<StoreUpdateReply> Update(StoreUpdateRequest request, ServerCallContext context)
    {
        return RunWithErrorHandling(() =>
        {
            var metadata = _registry.Find(request.Name);
            _registry.Update(request.Name, UnitHelpers.Parse(metadata, request.Value));
            return new StoreUpdateReply();
        });
    }

    public override Task<GetResponse> Get(GetRequest request, ServerCallContext context)
    {
        return RunWithErrorHandling(() =>
        {
            var value = _registry.GetCurrentValue(request.Name);
            if (value is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"No measure registered with the name '{request.Name}'."));

            return new GetResponse { Value = ToQuantityProto(value) };
        });
    }

    public override Task<GetManyResponse> GetMany(GetManyRequest request, ServerCallContext context)
    {
        return RunWithErrorHandling(() =>
        {
            var response = new GetManyResponse();
            foreach (var name in request.Names)
            {
                var value = _registry.GetCurrentValue(name);
                if (value is not null)
                    response.Values.Add(name, ToQuantityProto(value));
            }

            return response;
        });
    }

    public override async Task SubscribeToSet(SubscribeToSetRequest request, IServerStreamWriter<StoreChangeResponse> responseStream, ServerCallContext context)
    {
        var names = new HashSet<string>(request.Names);
        await HandleSubscription(responseStream, context, names.Contains, $"set = {string.Join(", ", names)})"
        );
    }

    public override async Task SubscribeTo(SubscribeToRequest request, IServerStreamWriter<StoreChangeResponse> responseStream, ServerCallContext context)
    {
        await HandleSubscription(
            responseStream, context,  name => name.Equals(request.Name, StringComparison.Ordinal), $"name = {request.Name}");
    }

    public override async Task SubscribeToMatching(SubscribeToMatchingRequest request, IServerStreamWriter<StoreChangeResponse> responseStream, ServerCallContext context)
    {
        try
        {
            var regex = new Regex(TextHelpers.GitLikeWildcardToRegex(request.Pattern), RegexOptions.Compiled);
            await HandleSubscription(responseStream, context, regex.IsMatch, $"pattern = {request.Pattern}");
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid pattern: {e.Message}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Call to SubscribeToMatching() failed ({Exception}): {Reason}", e.GetType().Name, e.Message);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }

    private async Task HandleSubscription(
        IServerStreamWriter<StoreChangeResponse> responseStream,
        ServerCallContext context,
        Func<string, bool> nameMatcher,
        string subscriptionIdentifier)
    {
        _logger.LogDebug("Client subscribed to '{Subscription}'", subscriptionIdentifier);
        var tcs = new TaskCompletionSource();

        EventHandler<ValueChangedEventArgs<IQuantity>> valueChangedHandler = async (_, args) =>
        {
            if (!nameMatcher(args.Name))
                return;

            try
            {
                var response = new StoreChangeResponse();
                response.Changes.Add(new StoreChangeResponse.Types.StoreChange
                {
                    Name = args.Name,
                    NewValue = ToQuantityProto(args.NewValue)
                });

                await responseStream.WriteAsync(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send update for subscription '{Subscription}'. Client may have disconnected.", subscriptionIdentifier);
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

        _logger.LogDebug("Client unsubscribed from '{Subscription}'", subscriptionIdentifier);
    }

    private static Tinkwell.Services.Quantity ToQuantityProto(IQuantity quantity)
    {
        bool isDouble = quantity.Value.Type == QuantityValue.UnderlyingDataType.Double;
        return new Tinkwell.Services.Quantity
        {
            Unit = quantity.Unit.ToString(),
            QuantityType = quantity.QuantityInfo.Name,
            Number = isDouble ? (double)quantity.Value : (double)(decimal)quantity.Value
        };
    }

    private readonly ILogger<StoreService> _logger;
    private readonly IRegistry _registry;

    private Task<TResult> RunWithErrorHandling<TResult>(Func<TResult> action, [CallerMemberName] string? callerName = null)
    {
        try
        {
            return Task.FromResult(action());
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message));
        }
        catch (KeyNotFoundException e)
        {
            throw new RpcException(new Status(StatusCode.NotFound, e.Message));
        }
        catch (NotSupportedException e)
        {
            // Most likely thrown when trying to convert between incompatible units
            throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message));
        }
        catch (InvalidOperationException e)
        {
            _logger.LogError(e, "Call to {Name}() failed ({Exception}): {Reason}", callerName, e.GetType().Name, e.Message);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, e.Message));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Call to {Name}() failed ({Exception}): {Reason}", callerName, e.GetType().Name, e.Message);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }
}
