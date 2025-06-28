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
            result.Items.AddRange(_registry.FindAll().Select(x =>
            {
                var item = new StoreListReply.Types.Item()
                {
                    Name = x.Name,
                    QuantityType = x.QuantityType,
                    Unit = x.Unit,
                };

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
            });

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

    public override async Task SubscribeTo(SubscribeToRequest request, IServerStreamWriter<StoreChangeResponse> responseStream, ServerCallContext context)
    {
        await HandleSubscription(
            responseStream,
            context,
            name => string.Equals(name, request.Name, StringComparison.Ordinal),
            $"name = {request.Name}"
        );
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
