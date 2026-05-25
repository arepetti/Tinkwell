using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Implements <see cref="ICoapRouteBuilder"/> by collecting code-driven
/// routes and mapping them onto a <see cref="CoapServer"/>.
/// </summary>
internal sealed class CoapRouteBuilderAdapter : ICoapRouteBuilder
{
    private readonly List<(string Pattern, byte? Verb, ICoapResourceHandler Handler)> _routes = new();

    public ICoapRouteBuilder MapGet(string pattern, ICoapResourceHandler handler)
    {
        _routes.Add((pattern, CoapCode.Get, handler));
        return this;
    }

    public ICoapRouteBuilder MapPut(string pattern, ICoapResourceHandler handler)
    {
        _routes.Add((pattern, CoapCode.Put, handler));
        return this;
    }

    public ICoapRouteBuilder MapPost(string pattern, ICoapResourceHandler handler)
    {
        _routes.Add((pattern, CoapCode.Post, handler));
        return this;
    }

    public ICoapRouteBuilder MapDelete(string pattern, ICoapResourceHandler handler)
    {
        _routes.Add((pattern, CoapCode.Delete, handler));
        return this;
    }

    public ICoapRouteBuilder Map(string pattern, ICoapResourceHandler handler)
    {
        _routes.Add((pattern, null, handler));
        return this;
    }

    /// <summary>
    /// Applies all collected routes to the CoAP server, wrapping each handler
    /// with the middleware pipeline.
    /// </summary>
    internal void ApplyTo(
        CoapServer server,
        IReadOnlyList<ICoapRequestMiddleware> middlewares,
        ILogger logger)
    {
        foreach (var (pattern, verb, handler) in _routes)
        {
            var adapted = new CodeDrivenCoapHandler(handler, middlewares, logger);

            if (verb is null)
            {
                server.Map(pattern, adapted);
            }
            else
            {
                Func<CoapRequest, CancellationToken, Task<CoapResponse>> func =
                    adapted.HandleAsync;
                switch (verb.Value)
                {
                    case CoapCode.Get:
                        server.MapGet(pattern, func);
                        break;
                    case CoapCode.Put:
                        server.MapPut(pattern, func);
                        break;
                    case CoapCode.Post:
                        server.MapPost(pattern, func);
                        break;
                    case CoapCode.Delete:
                        server.MapDelete(pattern, func);
                        break;
                    default:
                        server.Map(pattern, adapted);
                        break;
                }
            }
        }
    }
}
