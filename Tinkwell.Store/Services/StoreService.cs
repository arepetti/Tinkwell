using System.Globalization;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Services;

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
