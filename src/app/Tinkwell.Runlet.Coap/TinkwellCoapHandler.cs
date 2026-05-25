using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Runlet.Coap.Configuration;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Bridges a Tinkwell binding chain to a <see cref="CoapServer"/> by
/// implementing <see cref="ICoapRequestHandler"/>. Emits OT metrics/traces
/// for each request.
/// </summary>
internal sealed class TinkwellCoapHandler : ICoapRequestHandler
{
    private readonly CoapServerDefinition _server;
    private readonly CoapResourceDefinition _resource;
    private readonly BindingChainExecutor _executor;
    private readonly ILogger _logger;

    public TinkwellCoapHandler(
        CoapServerDefinition server,
        CoapResourceDefinition resource,
        BindingChainExecutor executor,
        ILogger logger)
    {
        _server = server;
        _resource = resource;
        _executor = executor;
        _logger = logger;
    }

    public async Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
    {
        OtMetrics.Requests.Add(1,
            new("coap.server", _server.Name),
            new("coap.method", CoapCode.ToMethodString((byte)request.Method)),
            new("coap.path", request.Path));

        using var activity = OtTraces.Source.StartActivity("coap.request");
        activity?.SetTag(OtTraces.ServerName, _server.Name);
        activity?.SetTag(OtTraces.Method, CoapCode.ToMethodString((byte)request.Method));
        activity?.SetTag(OtTraces.Path, request.Path);

        var stopwatch = Stopwatch.StartNew();
        byte responseCode;
        byte[]? responsePayload = null;
        CoapContentFormat? contentFormat = null;

        try
        {
            var (code, result) = await _executor.ExecuteAsync(_resource, request, ct);
            responseCode = code;
            if (result is not null)
            {
                responsePayload = result.Content;
                contentFormat = result.ContentFormat;
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request on {Path}", request.Path);
            activity?.SetTag(OtTraces.ResponseCode, CoapCode.ToDisplayString(CoapCode.BadRequest));
            return CoapResponse.BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal error processing CoAP request on {Path}", request.Path);
            activity?.SetTag(OtTraces.ResponseCode, CoapCode.ToDisplayString(CoapCode.InternalServerError));
            return CoapResponse.InternalError("Internal Server Error");
        }
        finally
        {
            OtMetrics.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new("coap.server", _server.Name),
                new("coap.path", request.Path));
        }

        activity?.SetTag(OtTraces.ResponseCode, CoapCode.ToDisplayString(responseCode));

        return new CoapResponse
        {
            Code = responseCode,
            Payload = responsePayload,
            ContentFormat = contentFormat,
        };
    }
}