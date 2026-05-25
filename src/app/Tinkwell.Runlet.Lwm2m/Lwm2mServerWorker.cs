using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Runlet.Lwm2m.Configuration;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// UDP listener for an LwM2M server instance. Receives CoAP datagrams,
/// parses them, and routes to <see cref="Lwm2mRequestDispatcher"/>.
/// The LwM2M protocol runs over CoAP (RFC 7252) as defined in
/// OMA-TS-LightweightM2M_Transport-V1_1, Section 6.
/// </summary>
internal sealed class Lwm2mServerWorker : BackgroundService
{
    private readonly Lwm2mServerDefinition _server;
    private readonly Lwm2mRequestDispatcher _dispatcher;
    private readonly ILogger _logger;

    public Lwm2mServerWorker(
        Lwm2mServerDefinition server,
        Lwm2mRequestDispatcher dispatcher,
        ILogger logger)
    {
        _server = server;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(_server.Port);
        _logger.LogInformation("LwM2M server '{Name}' listening on UDP port {Port}",
            _server.Name, _server.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try
            {
                datagram = await udp.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "UDP receive error on '{Name}'", _server.Name);
                continue;
            }

            _ = ProcessDatagramAsync(udp, datagram, stoppingToken);
        }

        _logger.LogInformation("LwM2M server '{Name}' stopped", _server.Name);
    }

    private async Task ProcessDatagramAsync(
        UdpClient udp, UdpReceiveResult datagram, CancellationToken ct)
    {
        CoapMessage request;
        try
        {
            request = CoapMessage.Parse(datagram.Buffer);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Malformed CoAP message from {Endpoint}",
                datagram.RemoteEndPoint);
            return;
        }

        if (request.Type is CoapMessageType.Acknowledgement or CoapMessageType.Reset)
            return;

        OtMetrics.Requests.Add(1,
            new("lwm2m.server", _server.Name),
            new("lwm2m.method", CoapCode.ToMethodString(request.Code)),
            new("lwm2m.path", request.UriPath));

        using var activity = OtTraces.Source.StartActivity("lwm2m.request");
        activity?.SetTag(OtTraces.ServerName, _server.Name);
        activity?.SetTag(OtTraces.Method, CoapCode.ToMethodString(request.Code));
        activity?.SetTag(OtTraces.Path, request.UriPath);

        var (responseCode, responsePayload, contentFormat) =
            _dispatcher.HandleRequest(request, datagram.RemoteEndPoint);

        activity?.SetTag(OtTraces.ResponseCode, CoapCode.ToDisplayString(responseCode));

        var responseType = request.Type == CoapMessageType.Confirmable
            ? CoapMessageType.Acknowledgement
            : CoapMessageType.NonConfirmable;

        var response = CoapMessage.BuildResponse(
            responseType, responseCode, request.MessageId,
            request.Token, contentFormat, responsePayload);

        try
        {
            await udp.SendAsync(response, response.Length, datagram.RemoteEndPoint);
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "Failed to send CoAP response to {Endpoint}",
                datagram.RemoteEndPoint);
        }
    }
}
