using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Runner;

namespace Tinkwell.Integration.Coap;

/// <summary>
/// Integration binding that sends a CoAP <b>confirmable (CON)</b> request (POST, PUT, or
/// DELETE) over UDP, waits for a response within the configured timeout, and optionally
/// returns a parsed text payload. Usable from MQTT, CoAP (forwarding), or any other
/// protocol context; does not use gRPC service discovery.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>path</c> (required) — the CoAP URI path (e.g. <c>/sensor/temperature</c>).</item>
///   <item><c>method</c> (optional, default <c>"post"</c>) — CoAP method: post, put, or delete.</item>
///   <item><c>host</c> (optional, default <c>"localhost"</c>) — target hostname or IP.</item>
///   <item><c>port</c> (optional, default <c>5683</c>) — target UDP port.</item>
///   <item><c>timeout</c> (optional, default <c>5</c>) — response wait in seconds.</item>
/// </list>
/// The request payload is taken from <see cref="IntegrationContext.Payload"/>. Non-success
/// response classes are logged as warnings.
/// </remarks>
public sealed class CoapBinding : ICoapIntegrationBinding, IMqttIntegrationBinding
{
    private readonly ILogger<CoapBinding>? _logger;

    public CoapBinding(IServiceDiscovery discovery, ILogger<CoapBinding>? logger = null)
    {
        _ = discovery;
        _logger = logger;
    }

    public string Name => "coap";

    public Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        SendAsync(context, parameters, evaluator, ct);

    public Task<BindingResult?> HandleCoapAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct) =>
        SendAsync(context, parameters, evaluator, ct);

    public Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        SendAsync(context, parameters, evaluator, ct);

    private async Task<BindingResult?> SendAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();

        var path = await BindingParameterResolver.ResolveRequiredAsync("path", "CoAP", parameters, evaluator, p, ct);
        var methodStr = await BindingParameterResolver.ResolveOptionalAsync("method", parameters, evaluator, p, ct) ?? "post";
        var host = await BindingParameterResolver.ResolveOptionalAsync("host", parameters, evaluator, p, ct) ?? "localhost";
        var portStr = await BindingParameterResolver.ResolveOptionalAsync("port", parameters, evaluator, p, ct);
        var timeoutStr = await BindingParameterResolver.ResolveOptionalAsync("timeout", parameters, evaluator, p, ct);

        var port = portStr is not null
            && int.TryParse(portStr, CultureInfo.InvariantCulture, out var portVal) ? portVal : 5683;
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

        var payload = context.Payload;
        var packet = CoapPacket.Build(methodCode, path, payload);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var udp = new UdpClient();
        var addresses = await Dns.GetHostAddressesAsync(host, cts.Token);
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException($"Could not resolve host '{host}'");
        }

        var endpoint = new IPEndPoint(addresses[0], port);

        await udp.SendAsync(packet, endpoint, cts.Token);
        var result = await udp.ReceiveAsync(cts.Token);

        var (cls, detail) = CoapPacket.ParseResponseCode(result.Buffer);

        _logger?.LogDebug("coap binding: {Method} {Path} → {Class}.{Detail:D2}",
            methodStr.ToUpperInvariant(), path, cls, detail);

        if (cls != 2)
        {
            _logger?.LogWarning("CoAP request {Method} {Path} returned {Class}.{Detail:D2}",
                methodStr.ToUpperInvariant(), path, cls, detail);
        }

        if (result.Buffer.Length > 4)
        {
            var responsePayload = ExtractPayload(result.Buffer);
            if (responsePayload.Length > 0)
            {
                return new BindingResult(responsePayload, CoapContentFormat.TextPlain);
            }
        }

        return null;
    }

    private const int MinHeaderLength = 4;
    private const int TokenLengthMask = 0x0F;
    private const byte PayloadMarker = 0xFF;
    private const int NibbleReserved = 15;

    /// <summary>RFC 7252 extended encoding thresholds (shared with <see cref="CoapPacket"/>).</summary>
    private const int ExtendedOneByte = 13;
    private const int ExtendedTwoBytes = 269;

    internal static byte[] ExtractPayload(byte[] data)
    {
        if (data.Length < MinHeaderLength)
        {
            return [];
        }

        byte tokenLength = (byte)(data[0] & TokenLengthMask);
        int offset = MinHeaderLength + tokenLength;

        while (offset < data.Length)
        {
            if (data[offset] == PayloadMarker)
            {
                break;
            }

            byte optHeader = data[offset++];
            int deltaNibble = (optHeader >> 4) & TokenLengthMask;
            int lengthNibble = optHeader & TokenLengthMask;
            if (deltaNibble == NibbleReserved || lengthNibble == NibbleReserved)
            {
                return [];
            }

            if (!TryReadOptionValue(deltaNibble, data, ref offset, out _))
            {
                return [];
            }

            if (!TryReadOptionValue(lengthNibble, data, ref offset, out int valueLength))
            {
                return [];
            }

            if (valueLength < 0 || offset + valueLength > data.Length)
            {
                return [];
            }

            offset += valueLength;
        }

        if (offset < data.Length && data[offset] == PayloadMarker)
        {
            return data[(offset + 1)..];
        }

        return [];
    }

    /// <summary>
    /// Decodes a CoAP option delta or length nibble and extended value per RFC 7252.
    /// Advances <paramref name="offset"/> only for extended (13/14) encodings.
    /// </summary>
    internal static bool TryReadOptionValue(
        int nibble, byte[] data, ref int offset, out int value)
    {
        if (nibble < ExtendedOneByte)
        {
            value = nibble;
            return true;
        }

        if (nibble == NibbleReserved)
        {
            value = 0;
            return false;
        }

        if (nibble == ExtendedOneByte)
        {
            if (offset >= data.Length)
            {
                value = 0;
                return false;
            }

            value = data[offset++] + ExtendedOneByte;
            return true;
        }

        // nibble == 14 (two-byte extended)
        if (offset + 1 >= data.Length)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2)) + ExtendedTwoBytes;
        offset += 2;
        return true;
    }
}
