using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Adapts an <see cref="ICoapResourceHandler"/> registered via
/// <see cref="ICoapBindingProvider"/> to <see cref="ICoapRequestHandler"/>
/// so it can be plugged into the <see cref="CoapServer"/> route table.
/// Bridges between <see cref="CoapRequestContext"/> (middleware) and
/// <see cref="IntegrationContext"/> (binding API).
/// </summary>
internal sealed class CodeDrivenCoapHandler : ICoapRequestHandler
{
    private readonly ICoapResourceHandler _inner;
    private readonly IReadOnlyList<ICoapRequestMiddleware> _middlewares;
    private readonly ILogger _logger;

    public CodeDrivenCoapHandler(
        ICoapResourceHandler inner,
        IReadOnlyList<ICoapRequestMiddleware> middlewares,
        ILogger logger)
    {
        _inner = inner;
        _middlewares = middlewares;
        _logger = logger;
    }

    public async Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
    {
        var coapCtx = BuildCoapContext(request);

        Func<CoapRequestContext, CancellationToken, Task<CoapMiddlewareResult?>> pipeline =
            async (ctx, token) =>
            {
                var integrationCtx = ToIntegrationContext(ctx);
                var bindingResult = await _inner.HandleAsync(integrationCtx, token);
                if (bindingResult is null)
                    return null;
                return new CoapMiddlewareResult(bindingResult.Content, bindingResult.ContentFormat);
            };

        for (int i=_middlewares.Count - 1; i >= 0; --i)
        {
            var mw = _middlewares[i];
            var next = pipeline;
            pipeline = (ctx, token) => mw.InvokeAsync(ctx, next, token);
        }

        CoapMiddlewareResult? result;
        try
        {
            result = await pipeline(coapCtx, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request on {Path}", request.Path);
            return CoapResponse.BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in code-driven handler for {Path}", request.Path);
            return CoapResponse.InternalError("Internal Server Error");
        }

        if (result is null)
        {
            return new CoapResponse
            {
                Code = DefaultSuccessCode((byte)request.Method),
            };
        }

        return new CoapResponse
        {
            Code = CoapCode.Content,
            Payload = result.Content,
            ContentFormat = result.ContentFormat,
        };
    }

    internal static CoapRequestContext BuildCoapContext(CoapRequest request)
    {
        var method = CoapCode.ToMethodString((byte)request.Method);
        var payloadString = request.Payload.Length > 0
            ? Encoding.UTF8.GetString(request.Payload.Span)
            : null;

        return new CoapRequestContext
        {
            Path = request.Path,
            Query = request.Query,
            Payload = payloadString,
            Method = method,
            PayloadBytes = request.Payload.Length > 0 ? request.Payload.ToArray() : null,
            RequestContentFormat = request.ContentFormat,
            Peer = new PeerIdentity(request.RemoteEndpoint),
        };
    }

    internal static IntegrationContext BuildContext(CoapRequest request)
    {
        var method = CoapCode.ToMethodString((byte)request.Method);
        var payloadString = request.Payload.Length > 0
            ? Encoding.UTF8.GetString(request.Payload.Span)
            : null;

        return new IntegrationContext(request.Path, request.Query, payloadString, method)
        {
            PayloadBytes = request.Payload.Length > 0 ? request.Payload.ToArray() : null,
            RequestContentFormat = request.ContentFormat,
            Peer = new PeerIdentity(request.RemoteEndpoint),
        };
    }

    private static IntegrationContext ToIntegrationContext(CoapRequestContext ctx)
    {
        var ic = new IntegrationContext(ctx.Path, ctx.Query, ctx.Payload, ctx.Method)
        {
            PayloadBytes = ctx.PayloadBytes,
            RequestContentFormat = ctx.RequestContentFormat,
            Peer = ctx.Peer,
        };
        foreach (var kv in ctx.Items)
            ic.Items[kv.Key] = kv.Value;
        return ic;
    }

    private static byte DefaultSuccessCode(byte requestCode) => requestCode switch
    {
        CoapCode.Post => CoapCode.Created,
        CoapCode.Put => CoapCode.Changed,
        CoapCode.Delete => CoapCode.Deleted,
        _ => CoapCode.Content,
    };
}