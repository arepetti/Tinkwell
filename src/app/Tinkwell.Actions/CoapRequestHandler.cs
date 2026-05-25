using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Actions.Abstractions;

namespace Tinkwell.Actions.Coap;

/// <summary>
/// Action handler that sends a CoAP request (POST, PUT, or DELETE) to a UDP endpoint.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>path</c> (required) — the CoAP URI path (e.g. <c>/sensor/temperature</c>).</item>
///   <item><c>method</c> (optional, default <c>"post"</c>) — CoAP method: post, put, or delete.</item>
///   <item><c>payload</c> (optional) — the request payload.</item>
///   <item><c>host</c> (optional, default <c>"localhost"</c>) — target hostname or IP.</item>
///   <item><c>port</c> (optional, default <c>5683</c>) — target UDP port.</item>
///   <item><c>timeout</c> (optional, default <c>5</c>) — response timeout in seconds.</item>
/// </list>
/// </remarks>
public sealed class CoapRequestHandler : IActionHandler
{
    private readonly ILogger<CoapRequestHandler> _logger;

    public CoapRequestHandler(IServiceDiscovery discovery, ILogger<CoapRequestHandler> logger)
    {
        _ = discovery;
        _logger = logger;
    }

    public string Name => "coap-request";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var path = await ActionParameterResolver.ResolveRequiredAsync(
            "path", parameters, trigger, evaluator, ct);
        var methodStr = await ActionParameterResolver.ResolveOptionalAsync(
            "method", parameters, trigger, evaluator, ct) ?? "post";
        var payload = await ActionParameterResolver.ResolveOptionalAsync(
            "payload", parameters, trigger, evaluator, ct);
        var host = await ActionParameterResolver.ResolveOptionalAsync(
            "host", parameters, trigger, evaluator, ct) ?? "localhost";
        var portStr = await ActionParameterResolver.ResolveOptionalAsync(
            "port", parameters, trigger, evaluator, ct);
        var timeoutStr = await ActionParameterResolver.ResolveOptionalAsync(
            "timeout", parameters, trigger, evaluator, ct);

        var port = portStr is not null
            && int.TryParse(portStr, CultureInfo.InvariantCulture, out var p) ? p : 5683;
        var timeout = timeoutStr is not null
            && int.TryParse(timeoutStr, CultureInfo.InvariantCulture, out var t) ? t : 5;

        byte methodCode = methodStr.ToLowerInvariant() switch
        {
            "post" => CoapPacket.MethodPost,
            "put" => CoapPacket.MethodPut,
            "delete" => CoapPacket.MethodDelete,
            _ => throw new InvalidOperationException(
                $"Unsupported CoAP method '{methodStr}'. Use post, put, or delete."),
        };

        var packet = CoapPacket.Build(methodCode, path, payload);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var udp = new UdpClient();
        var addresses = await Dns.GetHostAddressesAsync(host, cts.Token);
        var endpoint = new IPEndPoint(addresses[0], port);

        await udp.SendAsync(packet, endpoint, cts.Token);
        var result = await udp.ReceiveAsync(cts.Token);

        var (cls, detail) = CoapPacket.ParseResponseCode(result.Buffer);

        _logger.LogDebug("coap-request: {Method} {Path} → {Class}.{Detail:D2}",
            methodStr.ToUpperInvariant(), path, cls, detail);

        if (cls != 2)
            _logger.LogWarning("CoAP request {Method} {Path} returned {Class}.{Detail:D2}",
                methodStr.ToUpperInvariant(), path, cls, detail);
    }
}
