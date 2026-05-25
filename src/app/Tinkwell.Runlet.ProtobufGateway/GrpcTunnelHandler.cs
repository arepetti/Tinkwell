using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// CoAP request handler that tunnels raw protobuf bytes to a discovered
/// gRPC service. Enforces POST-only, validates the path against a
/// <see cref="ServiceWhitelist"/>, runs the <see cref="IGatewayMiddleware"/>
/// pipeline, and maps gRPC status codes to CoAP responses.
/// </summary>
internal sealed class GrpcTunnelHandler : ICoapRequestHandler
{
    private static readonly Marshaller<byte[]> IdentityMarshaller = new(
        static bytes => bytes,
        static bytes => bytes);

    private readonly PathTemplate _template;
    private readonly ServiceWhitelist _whitelist;
    private readonly ServiceCache _cache;
    private readonly IReadOnlyList<IGatewayMiddleware> _middlewares;
    private readonly string _profileName;
    private readonly ILogger _logger;

    public GrpcTunnelHandler(
        PathTemplate template,
        ServiceWhitelist whitelist,
        ServiceCache cache,
        IReadOnlyList<IGatewayMiddleware> middlewares,
        string profileName,
        ILogger logger)
    {
        _template = template;
        _whitelist = whitelist;
        _cache = cache;
        _middlewares = middlewares;
        _profileName = profileName;
        _logger = logger;
    }

    public async Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
    {
        if (request.Method != CoapMethod.Post)
            return CoapResponse.MethodNotAllowed();

        if (!_template.TryExtract(request.Path, out var service, out var method))
            return CoapResponse.BadRequest("Malformed path: cannot extract service/method.");

        if (!_whitelist.IsAllowed(service))
        {
            _logger.LogDebug("Service '{Service}' denied by whitelist", service);
            return CoapResponse.Forbidden($"Service '{service}' is not allowed.");
        }

        var context = new GatewayRequestContext
        {
            Service = service,
            Method = method,
            ProfileName = _profileName,
            Request = request,
        };

        if (_middlewares.Count > 0)
            return await RunPipeline(context, ct);

        return await TunnelAsync(context, ct);
    }

    private Task<CoapResponse> RunPipeline(GatewayRequestContext context, CancellationToken ct)
    {
        Func<GatewayRequestContext, CancellationToken, Task<CoapResponse>> pipeline = TunnelAsync;

        for (int i=_middlewares.Count - 1; i >= 0; --i)
        {
            var mw = _middlewares[i];
            var next = pipeline;
            pipeline = (ctx, token) => mw.InvokeAsync(ctx, next, token);
        }

        return pipeline(context, ct);
    }

    private async Task<CoapResponse> TunnelAsync(
        GatewayRequestContext context, CancellationToken ct)
    {
        GrpcChannel? channel;
        try
        {
            channel = await _cache.GetChannelAsync(context.Service, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service discovery failed for '{Service}'", context.Service);
            return ServiceUnavailable();
        }

        if (channel is null)
        {
            _logger.LogWarning("Service '{Service}' not found via discovery", context.Service);
            return CoapResponse.NotFound();
        }

        var grpcMethod = new Method<byte[], byte[]>(
            MethodType.Unary,
            context.Service,
            context.Method,
            IdentityMarshaller,
            IdentityMarshaller);

        try
        {
            var invoker = channel.CreateCallInvoker();
            var payload = context.Request.Payload.ToArray();
            var response = await invoker.AsyncUnaryCall(grpcMethod, null, default, payload);
            return CoapResponse.Content(response, CoapContentFormat.ApplicationOctetStream);
        }
        catch (RpcException ex)
        {
            _logger.LogDebug(ex, "gRPC call to {Service}/{Method} failed: {Status}",
                context.Service, context.Method, ex.StatusCode);
            return MapGrpcError(ex.StatusCode);
        }
    }

    private static CoapResponse MapGrpcError(StatusCode code) => code switch
    {
        StatusCode.InvalidArgument => CoapResponse.BadRequest("gRPC: InvalidArgument"),
        StatusCode.NotFound => CoapResponse.NotFound(),
        StatusCode.PermissionDenied => CoapResponse.Forbidden("gRPC: PermissionDenied"),
        StatusCode.Unavailable => ServiceUnavailable(),
        _ => CoapResponse.InternalError($"gRPC: {code}"),
    };

    private static CoapResponse ServiceUnavailable() => new()
    {
        Code = CoapCode.ServiceUnavailable,
    };
}