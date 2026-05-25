using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace Tinkwell.Coap;

/// <summary>
/// Stateless CoAP client: sends a Confirmable request over UDP, waits for the response, and
/// transparently handles Block1 (large request) and Block2 (large response) transfers per RFC 7959.
/// </summary>
/// <remarks>
/// <para>
/// <b>Confirmable-only in 1.0.</b> This client always sends Confirmable (<c>CON</c>) requests and
/// relies on the CoAP retransmission state machine (RFC 7252, Section 4.2) for reliability.
/// Non-confirmable (<c>NON</c>) request support is planned for a later release; callers that need
/// it today must build it on top of <see cref="CoapMessage"/> and a raw UDP socket.
/// </para>
/// <para>
/// This client is intentionally minimal. It opens a short-lived <c>UdpClient</c> per call (IPv6
/// dual-stack so one socket can reach IPv4 and IPv6 peers), performs
/// one or more CoAP exchanges (as required by blockwise transfers), and returns the reassembled
/// response. Incoming datagrams are accepted only when they arrive from the resolved target
/// endpoint and carry the same Token as the request; piggy-backed <see cref="CoapMessageType.Acknowledgement"/>
/// responses must also echo the outgoing Message ID. Wrong peers, tokens, or MIDs are discarded and
/// receiving continues until a match arrives or timeouts / retransmissions exhaust (RFC 7252, Section 4.2 for CON).
/// Datagrams whose parsed version is not 1 (per RFC 7252, Section 3) are rejected by
/// <see cref="CoapMessage.Parse(System.ReadOnlySpan{byte})"/> with <see cref="FormatException"/>; within
/// <see cref="CoapClient"/>, such datagrams from the target peer are silently discarded just like any
/// other malformed or non-matching response, and the client keeps listening or retransmits per RFC 7252, Section 4.2.
/// Malformed or non-matching datagrams (including bad versions, truncated messages, and unrelated traffic)
/// from the target peer are not surfaced to callers: they are dropped and the client continues waiting until
/// a matching response arrives, a timeout or cancellation occurs, or Confirmable retransmissions are exhausted.
/// On some platforms (notably Windows), ICMP errors such as "port unreachable" may surface as
/// <see cref="SocketException"/> during <c>ReceiveAsync</c> instead of timing out.
/// </para>
/// <para>
/// When DNS resolves multiple addresses for a host name, the client orders them with IPv4
/// (<see cref="AddressFamily.InterNetwork"/>) first, then IPv6 (<see cref="AddressFamily.InterNetworkV6"/>),
/// preserving the resolver order within each family. It tries each address in turn: the full exchange
/// (including retransmissions and blockwise transfers) runs against that address; if the attempt ends with
/// <see cref="TimeoutException"/> or <see cref="SocketException"/> (for example send failures such as
/// address unreachable) and another address remains, it tries the next without logging. <see cref="OperationCanceledException"/>
/// (user cancellation or <see cref="CoapClientRequestOptions.TotalTimeout"/> expiry; includes
/// <see cref="TaskCanceledException"/> when the token is already canceled before work starts) is never retried on another address.
/// </para>
/// <para>
/// It does not implement Observe subscriptions (RFC 7641), DTLS, or multicast discovery—callers that need
/// those must build them on top of <see cref="CoapMessage"/> directly.
/// </para>
/// <para>
/// The three overloads differ only in how the target endpoint is specified; they all share the
/// same <see cref="CoapClientRequest"/> / <see cref="CoapClientRequestOptions"/> contract.
/// </para>
/// <example>
/// <code>
/// var response = await CoapClient.SendAsync(
///     new Uri("coap://device.local/sensors/temperature"),
///     new CoapClientRequest { Method = CoapMethod.Get, Accept = CoapContentFormat.ApplicationJson },
///     new CoapClientRequestOptions { Timeout = TimeSpan.FromSeconds(2) },
///     cancellationToken);
///
/// if (response.Code == CoapCode.Content)
///     Console.WriteLine(response.PayloadString);
/// </code>
/// </example>
/// </remarks>
public static class CoapClient
{
    /// <summary>
    /// Sends a CoAP request to the endpoint described by <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">Absolute CoAP URI (<c>coap://host[:port]/path[?query]</c>). Scheme is not validated - the transport is always plain UDP.</param>
    /// <param name="request">Request description (method, payload, options).</param>
    /// <param name="options">Transport options (timeouts, blockwise behaviour).</param>
    /// <param name="ct">Cancellation token. The linked token also fires when <see cref="CoapClientRequestOptions.TotalTimeout"/> elapses.
    /// Name resolution uses this token; a token already in the canceled state may throw
    /// <see cref="TaskCanceledException"/> (a subtype of <see cref="OperationCanceledException"/>).</param>
    /// <returns>The parsed response, with any Block2 payload already reassembled.</returns>
    /// <remarks>
    /// When <see cref="Uri.IsDefaultPort"/> is <see langword="true"/> or <see cref="Uri.Port"/> is <c>-1</c>,
    /// the UDP port defaults to 5683 (RFC 7252, Section 12.6). A leading <c>?</c> on
    /// <see cref="Uri.Query"/> is stripped before Uri-Query options are built; an empty or missing query
    /// omits Uri-Query options.
    /// Multi-address DNS behavior for <see cref="Uri.Host"/> matches
    /// <see cref="SendAsync(string, int, string, string?, CoapClientRequest, CoapClientRequestOptions, CancellationToken)"/>.
    /// </remarks>
    /// <example>
    /// <para>Send a GET to an absolute <c>coap://</c> URI (default port 5683 when omitted).</para>
    /// <code>
    /// var response = await CoapClient.SendAsync(
    ///     new Uri("coap://device.local/sensors/temperature"),
    ///     new CoapClientRequest { Method = CoapMethod.Get },
    ///     new CoapClientRequestOptions { Timeout = TimeSpan.FromSeconds(2) },
    ///     cancellationToken);
    /// if (response.Code == CoapCode.Content)
    ///     Console.WriteLine(response.PayloadString);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/>, <paramref name="request"/>, or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> fires, a per-exchange / total timeout elapses, or (for example) name resolution is canceled;
    /// <see cref="TaskCanceledException"/> is used when the token is already canceled before asynchronous work begins.
    /// </exception>
    /// <exception cref="TimeoutException">All Confirmable retransmissions are exhausted without a matching response (RFC 7252, Section 4.2).</exception>
    /// <exception cref="InvalidOperationException">Blockwise protocol failure: a Block2 response exceeds <see cref="CoapClientRequestOptions.MaxResponseBytes"/>, or a follow-up response is missing the Block2 option / carries a block number or size exponent that does not match the expected value (RFC 7959, Section 2.4).</exception>
    /// <exception cref="System.Net.Sockets.SocketException">
    /// A network or DNS error occurs, name resolution returns no addresses, or (on some hosts, notably Windows) ICMP errors
    /// during receive such as "port unreachable" surface from UDP.
    /// </exception>
    public static Task<CoapMessage> SendAsync(
        Uri uri,
        CoapClientRequest request,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        int port = uri.IsDefaultPort || uri.Port < 0 ? 5683 : uri.Port;
        string? query = NormalizeUriQuery(uri.Query);

        return SendAsync(
            uri.Host,
            port,
            uri.AbsolutePath,
            query,
            request,
            options,
            ct);
    }

    /// <summary>
    /// Sends a CoAP request using the default CoAP port (5683).
    /// </summary>
    /// <param name="host">Target hostname or IP address.</param>
    /// <param name="path">URI path (e.g. <c>/sensor/temperature</c>).</param>
    /// <param name="query">URI query string (e.g. <c>ep=device1&amp;lt=300</c>), or <see langword="null"/>.</param>
    /// <param name="request">Request description (method, payload, options).</param>
    /// <param name="options">Transport options (timeouts, blockwise behaviour).</param>
    /// <param name="ct">Cancellation token. The linked token also fires when <see cref="CoapClientRequestOptions.TotalTimeout"/> elapses.
    /// Name resolution uses this token; a token already in the canceled state may throw
    /// <see cref="TaskCanceledException"/> (a subtype of <see cref="OperationCanceledException"/>).</param>
    /// <returns>The parsed response, with any Block2 payload already reassembled.</returns>
    /// <remarks>
    /// When DNS resolves multiple addresses for <paramref name="host"/>, IPv4 is preferred and the client
    /// falls back to IPv6 after <see cref="TimeoutException"/> or send-time <see cref="SocketException"/>
    /// on earlier addresses; see class remarks. <see cref="OperationCanceledException"/> is not retried.
    /// </remarks>
    /// <example>
    /// <para>PUT a JSON payload to the default CoAP port (5683).</para>
    /// <code>
    /// var body = System.Text.Encoding.UTF8.GetBytes("{\"on\":true}");
    /// var response = await CoapClient.SendAsync(
    ///     "actuator.local",
    ///     "/switch/state",
    ///     query: null,
    ///     new CoapClientRequest
    ///     {
    ///         Method = CoapMethod.Put,
    ///         Payload = body,
    ///         ContentFormat = CoapContentFormat.ApplicationJson,
    ///     },
    ///     new CoapClientRequestOptions(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="host"/>, <paramref name="path"/>, <paramref name="request"/>, or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> fires, a per-exchange / total timeout elapses, or (for example) name resolution is canceled;
    /// <see cref="TaskCanceledException"/> is used when the token is already canceled before asynchronous work begins.
    /// </exception>
    /// <exception cref="TimeoutException">All Confirmable retransmissions are exhausted without a matching response (RFC 7252, Section 4.2).</exception>
    /// <exception cref="InvalidOperationException">Blockwise protocol failure: a Block2 response exceeds <see cref="CoapClientRequestOptions.MaxResponseBytes"/>, or a follow-up response is missing the Block2 option / carries a block number or size exponent that does not match the expected value (RFC 7959, Section 2.4).</exception>
    /// <exception cref="System.Net.Sockets.SocketException">
    /// A network or DNS error occurs, name resolution returns no addresses, or (on some hosts, notably Windows) ICMP errors
    /// during receive such as "port unreachable" surface from UDP.
    /// </exception>
    public static Task<CoapMessage> SendAsync(
        string host,
        string path,
        string? query,
        CoapClientRequest request,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        return SendAsync(host, 5683, path, query, request, options, ct);
    }

    /// <summary>
    /// Sends a CoAP request, transparently handling blockwise transfers (RFC 7959).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Block1 is used automatically when <see cref="CoapClientRequest.Payload"/> is larger than
    /// <see cref="CoapClientRequestOptions.RequestBlockSize"/>, or when
    /// <see cref="CoapClientRequestOptions.ForceBlockwise"/> is set on a non-empty payload.
    /// When <see cref="CoapClientRequestOptions.RequestBlockSize"/> is <see langword="null"/>,
    /// Block1 fragmentation is disabled entirely.
    /// </para>
    /// <para>
    /// Block2 reassembly is always performed when the server's response indicates more blocks
    /// are available. The returned <see cref="CoapMessage"/> carries the reassembled payload.
    /// Options (except Block2 and Size2) are taken from the <i>first</i> Block2 response; the
    /// payload is the concatenation of all blocks. Each follow-up response must echo the requested
    /// Block2 number and size exponent (RFC 7959, Section 2.4).
    /// When Block2 reassembly runs, the returned <see cref="CoapMessage"/> is a synthetic composite: the
    /// <see cref="CoapMessage.Payload"/> is the concatenation of all blocks; <see cref="CoapMessage.Options"/>
    /// are taken from the <i>first</i> response (minus Block2 and Size2); <see cref="CoapMessage.MessageId"/>,
    /// <see cref="CoapMessage.Type"/>, <see cref="CoapMessage.Code"/>, <see cref="CoapMessage.Token"/>, and
    /// <see cref="CoapMessage.Version"/> are taken from the <i>last</i> block response. Callers that correlate
    /// on Message ID should do so against the client's outgoing MID (available via <see cref="CoapClientRequest.MessageId"/>
    /// when they set it), not against the returned message.
    /// </para>
    /// <para>
    /// <b>Identifiers.</b> A single Token (RFC 7252, Section 5.3.1) is chosen once at the start of
    /// the call - either from <see cref="CoapClientRequest.Token"/> or a random 2-byte value - and
    /// is reused across every block exchange (Block1 upload, initial request, Block2 follow-ups),
    /// per RFC 7959, Section 2.4 guidance for blockwise transfers. Each wire message gets its own
    /// fresh Message ID (RFC 7252, Section 4.4 requires uniqueness within the exchange lifetime);
    /// <see cref="CoapClientRequest.MessageId"/> is honoured only for the very first exchange.
    /// Confirmable retransmissions repeat the same datagram (same Message ID and Token) per RFC 7252, Section 4.2.
    /// </para>
    /// <para>
    /// When DNS resolves multiple addresses for <paramref name="host"/>, IPv4 is preferred and the client
    /// falls back to IPv6 after <see cref="TimeoutException"/> or send-time <see cref="SocketException"/>
    /// on earlier addresses; see class remarks. <see cref="OperationCanceledException"/> is not retried.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Call a host on a non-default UDP port (e.g. a test server or LwM2M bootstrap on 5684).</para>
    /// <code>
    /// var response = await CoapClient.SendAsync(
    ///     "192.0.2.1",
    ///     port: 5684,
    ///     path: "/.well-known/core",
    ///     query: null,
    ///     new CoapClientRequest { Method = CoapMethod.Get },
    ///     new CoapClientRequestOptions { Timeout = TimeSpan.FromSeconds(2) },
    ///     cancellationToken);
    /// </code>
    /// </example>
    /// <param name="host">Target hostname or IP address.</param>
    /// <param name="port">Target UDP port (5683 for unencrypted CoAP per RFC 7252, Section 12.6).</param>
    /// <param name="path">URI path.</param>
    /// <param name="query">URI query string, or <see langword="null"/>.</param>
    /// <param name="request">Request description (method, payload, options).</param>
    /// <param name="options">Transport options (timeouts, blockwise behaviour).</param>
    /// <param name="ct">Cancellation token. The linked token also fires when <see cref="CoapClientRequestOptions.TotalTimeout"/> elapses.
    /// Name resolution uses this token; a token already in the canceled state may throw
    /// <see cref="TaskCanceledException"/> (a subtype of <see cref="OperationCanceledException"/>).</param>
    /// <returns>The parsed response, with any Block2 payload already reassembled.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="host"/>, <paramref name="path"/>, <paramref name="request"/>, or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> fires, a per-exchange / total timeout elapses, or (for example) name resolution is canceled;
    /// <see cref="TaskCanceledException"/> is used when the token is already canceled before asynchronous work begins.
    /// </exception>
    /// <exception cref="TimeoutException">All Confirmable retransmissions are exhausted without a matching response (RFC 7252, Section 4.2).</exception>
    /// <exception cref="InvalidOperationException">Blockwise protocol failure: a Block2 response exceeds <see cref="CoapClientRequestOptions.MaxResponseBytes"/>, or a follow-up response is missing the Block2 option / carries a block number or size exponent that does not match the expected value (RFC 7959, Section 2.4).</exception>
    /// <exception cref="System.Net.Sockets.SocketException">
    /// A network or DNS error occurs, name resolution returns no addresses, or (on some hosts, notably Windows) ICMP errors
    /// during receive such as "port unreachable" surface from UDP.
    /// </exception>
    public static async Task<CoapMessage> SendAsync(
        string host,
        int port,
        string path,
        string? query,
        CoapClientRequest request,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        using var opCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (options.TotalTimeout.HasValue)
            opCts.CancelAfter(options.TotalTimeout.Value);

        var opCt = opCts.Token;

        using var udp = CreateDualStackUdpClient();
        var addresses = await Dns.GetHostAddressesAsync(host, opCt).ConfigureAwait(false);
        if (addresses.Length == 0)
        {
            throw new SocketException(
                (int)SocketError.HostNotFound,
                $"No IP addresses were resolved for host '{host}'.");
        }

        var sorted = SortHostAddressesPreferIpv4(addresses);

        // One Token for the whole SendAsync call (RFC 7959, Section 2.4), including DNS fallbacks.
        byte[] operationToken = request.Token ?? NewToken();
        ushort firstMessageId = request.MessageId ?? NewMessageId();

        for (int i=0; i < sorted.Length; ++i)
        {
            var endpoint = new IPEndPoint(sorted[i], port);
            try
            {
                return await SendAsyncToEndpointAsync(
                        udp,
                        endpoint,
                        path,
                        query,
                        request,
                        options,
                        operationToken,
                        firstMessageId,
                        opCt)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                if (i < sorted.Length - 1)
                    continue;

                throw;
            }
            catch (SocketException)
            {
                if (i < sorted.Length - 1)
                    continue;

                throw;
            }
        }

        throw new UnreachableException("CoAP SendAsync: the resolved address list was checked for emptiness earlier and the loop always runs at least once or rethrows.");
    }

    private static UdpClient CreateDualStackUdpClient()
    {
        var udp = new UdpClient(AddressFamily.InterNetworkV6);
        udp.Client.DualMode = true;
        return udp;
    }

    /// <summary>
    /// Orders resolver results with IPv4 first, then IPv6, preserving order within each family.
    /// Other <see cref="AddressFamily"/> values follow IPv6 in their original relative order.
    /// </summary>
    private static IPAddress[] SortHostAddressesPreferIpv4(IPAddress[] addresses)
    {
        var v4 = new List<IPAddress>();
        var v6 = new List<IPAddress>();
        var other = new List<IPAddress>();
        foreach (var a in addresses)
        {
            switch (a.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    v4.Add(a);
                    break;
                case AddressFamily.InterNetworkV6:
                    v6.Add(a);
                    break;
                default:
                    other.Add(a);
                    break;
            }
        }

        if (v4.Count == 0 && v6.Count == 0)
            return other.Count == 0 ? addresses : [.. other];

        if (other.Count == 0)
        {
            if (v4.Count == 0)
                return [.. v6];
            if (v6.Count == 0)
                return [.. v4];
            var merged = new IPAddress[v4.Count + v6.Count];
            v4.CopyTo(merged);
            v6.CopyTo(merged, v4.Count);
            return merged;
        }

        var all = new List<IPAddress>(v4.Count + v6.Count + other.Count);
        all.AddRange(v4);
        all.AddRange(v6);
        all.AddRange(other);
        return [.. all];
    }

    private static async Task<CoapMessage> SendAsyncToEndpointAsync(
        UdpClient udp,
        IPEndPoint endpoint,
        string path,
        string? query,
        CoapClientRequest request,
        CoapClientRequestOptions options,
        byte[] operationToken,
        ushort firstMessageId,
        CancellationToken ct)
    {
        byte[] payload = request.Payload ?? [];

        bool useBlock1 = false;
        CoapBlockSize blockSize = default;
        if (options.RequestBlockSize is { } size)
        {
            blockSize = size;
            int blockBytes = (int)size;
            useBlock1 = payload.Length > blockBytes
                || (options.ForceBlockwise && payload.Length > 0);
        }

        CoapMessage response = useBlock1
            ? await SendBlock1Async(udp, endpoint, path, query, request, payload, blockSize, operationToken, firstMessageId, options, ct).ConfigureAwait(false)
            : await SendSingleAsync(udp, endpoint, path, query, request, payload, operationToken, firstMessageId, options, ct).ConfigureAwait(false);

        if (response.Block2 is { More: true } block2)
            response = await ReassembleBlock2Async(udp, endpoint, path, query, request, response, block2, operationToken, options, ct).ConfigureAwait(false);

        return response;
    }

    private static string? NormalizeUriQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        return query[0] == '?' ? query[1..] : query;
    }

    private static async Task<CoapMessage> SendSingleAsync(
        UdpClient udp,
        IPEndPoint endpoint,
        string path,
        string? query,
        CoapClientRequest request,
        byte[] payload,
        byte[] token,
        ushort messageId,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        var requestMessage = CoapMessage.BuildRequest(
            CoapMessageType.Confirmable,
            (byte)request.Method,
            messageId,
            token,
            path,
            query,
            request.ContentFormat,
            request.Accept,
            payload.Length == 0 ? null : payload);

        return await ExchangeWithRetransmitAsync(
            udp,
            endpoint,
            requestMessage,
            messageId,
            token,
            options,
            ct).ConfigureAwait(false);
    }

    private static async Task<CoapMessage> SendBlock1Async(
        UdpClient udp,
        IPEndPoint endpoint,
        string path,
        string? query,
        CoapClientRequest request,
        byte[] payload,
        CoapBlockSize requestBlockSize,
        byte[] token,
        ushort firstMessageId,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        int blockSize = (int)requestBlockSize;
        int szx = BitOperations.Log2((uint)blockSize) - 4;
        int totalBlocks = Math.Max(1, (payload.Length + blockSize - 1) / blockSize);

        CoapMessage response = null!;

        for (int num=0; num < totalBlocks; ++num)
        {
            int offset = num * blockSize;
            int chunkLen = Math.Min(blockSize, payload.Length - offset);
            bool more = num < totalBlocks - 1;

            var block1 = new CoapBlockOption(num, more, szx);
            var chunk = payload.AsSpan(offset, chunkLen).ToArray();

            ushort messageId = num == 0 ? firstMessageId : NewMessageId();

            var requestMessage = CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                (byte)request.Method,
                messageId,
                token,
                path,
                query,
                request.ContentFormat,
                num == totalBlocks - 1 ? request.Accept : null,
                chunk,
                block1: block1,
                size1: num == 0 ? payload.Length : null);

            response = await ExchangeWithRetransmitAsync(
                udp,
                endpoint,
                requestMessage,
                messageId,
                token,
                options,
                ct).ConfigureAwait(false);

            if (more && response.Code != CoapCode.Continue)
                return response;
        }

        return response;
    }

    private static async Task<CoapMessage> ReassembleBlock2Async(
        UdpClient udp,
        IPEndPoint endpoint,
        string path,
        string? query,
        CoapClientRequest request,
        CoapMessage first,
        CoapBlockOption firstBlock2,
        byte[] token,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        using var assembled = new MemoryStream();
        assembled.Write(first.Payload);
        EnforceMaxResponse(assembled.Length, options.MaxResponseBytes);

        int nextNum = firstBlock2.Number + 1;
        int szx = firstBlock2.SizeExponent;
        var response = first;

        var mergedOptions = OptionsFromFirstWithoutBlockwise(first);

        while (true)
        {
            var requestBlock2 = new CoapBlockOption(nextNum, false, szx);

            var messageId = NewMessageId();

            var requestMessage = CoapMessage.BuildRequest(
                CoapMessageType.Confirmable,
                (byte)request.Method,
                messageId,
                token,
                path,
                query,
                contentFormat: null,
                accept: request.Accept,
                payload: null,
                block2: requestBlock2);

            response = await ExchangeWithRetransmitAsync(
                udp,
                endpoint,
                requestMessage,
                messageId,
                token,
                options,
                ct).ConfigureAwait(false);

            if (response.Block2 is not { } incomingB2)
            {
                throw new InvalidOperationException(
                    "Blockwise follow-up response is missing the Block2 option (RFC 7959, Section 2.4).");
            }

            if (incomingB2.Number != nextNum)
            {
                throw new InvalidOperationException(
                    $"Block2 block number mismatch: expected {nextNum} per RFC 7959, Section 2.4, received {incomingB2.Number}.");
            }

            if (incomingB2.SizeExponent != szx)
            {
                throw new InvalidOperationException(
                    $"Block2 size exponent mismatch: expected {szx} (same as the first response) per RFC 7959, Section 2.4, received {incomingB2.SizeExponent}.");
            }

            assembled.Write(response.Payload);
            EnforceMaxResponse(assembled.Length, options.MaxResponseBytes);

            if (response.Block2 is not { More: true })
            {
                return new CoapMessage
                {
                    Version = response.Version,
                    Type = response.Type,
                    Code = response.Code,
                    MessageId = response.MessageId,
                    Token = response.Token,
                    Options = mergedOptions,
                    Payload = assembled.ToArray(),
                };
            }

            nextNum++;
        }
    }

    private static List<CoapOption> OptionsFromFirstWithoutBlockwise(CoapMessage first) =>
        first.Options
            .Where(o => o.Number is not CoapOptionNumber.Block2 and not CoapOptionNumber.Size2)
            .ToList();

    /// <summary>
    /// One Confirmable transmission attempt: send once, then receive until the attempt timer elapses,
    /// discarding datagrams that fail correlation (wrong endpoint, token, or MID for ACKs). Empty
    /// ACKs (separate response) consume a matching datagram but keep receiving. A matching CON
    /// response is ACKed before return.
    /// </summary>
    private static async Task<CoapMessage?> ExchangeSingleAttemptAsync(
        UdpClient udp,
        IPEndPoint targetEndpoint,
        byte[] requestDatagram,
        ushort outgoingMessageId,
        byte[] outgoingToken,
        TimeSpan attemptLimit,
        TimeSpan? perReceiveTimeout,
        CoapMessageParseLimits parseLimits,
        CancellationToken ct)
    {
        await SendUdpAsync(udp, requestDatagram, targetEndpoint, ct).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < attemptLimit)
        {
            ct.ThrowIfCancellationRequested();

            var remaining = attemptLimit - sw.Elapsed;
            var receiveBudget = perReceiveTimeout.HasValue
                ? TimeSpan.FromTicks(Math.Min(perReceiveTimeout.Value.Ticks, remaining.Ticks))
                : remaining;

            if (receiveBudget <= TimeSpan.Zero)
                return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(receiveBudget);

            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                continue;
            }

            if (!TryAcceptResponse(
                    result,
                    targetEndpoint,
                    outgoingMessageId,
                    outgoingToken,
                    parseLimits,
                    out var message,
                    out var needAck,
                    out var ackMid))
            {
                continue;
            }

            if (needAck && ackMid is { } mid)
            {
                var ack = CoapMessage.BuildResponse(
                    CoapMessageType.Acknowledgement,
                    0,
                    mid,
                    message.Token,
                    null,
                    null);
                await SendUdpAsync(udp, ack, targetEndpoint, ct).ConfigureAwait(false);
            }

            return message;
        }

        return null;
    }

    private static bool TryAcceptResponse(
        UdpReceiveResult result,
        IPEndPoint targetEndpoint,
        ushort outgoingMessageId,
        byte[] outgoingToken,
        CoapMessageParseLimits parseLimits,
        out CoapMessage message,
        out bool needAckToServer,
        out ushort? ackForIncomingConMid)
    {
        message = null!;
        needAckToServer = false;
        ackForIncomingConMid = null;

        if (!EndPointEquals(result.RemoteEndPoint, targetEndpoint))
            return false;

        CoapMessage parsed;
        try
        {
            parsed = CoapMessage.Parse(result.Buffer, parseLimits);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!parsed.Token.AsSpan().SequenceEqual(outgoingToken))
            return false;

        switch (parsed.Type)
        {
            case CoapMessageType.Acknowledgement:
                if (parsed.MessageId != outgoingMessageId)
                    return false;

                // Empty ACK (code 0.00) — first leg of a separate response; keep receiving.
                if (parsed.Code == 0)
                    return false;

                message = parsed;
                return true;

            case CoapMessageType.Confirmable:
                message = parsed;
                needAckToServer = true;
                ackForIncomingConMid = parsed.MessageId;
                return true;

            case CoapMessageType.NonConfirmable:
            case CoapMessageType.Reset:
                message = parsed;
                return true;

            default:
                return false;
        }
    }

    private static async Task<CoapMessage> ExchangeWithRetransmitAsync(
        UdpClient udp,
        IPEndPoint targetEndpoint,
        byte[] requestDatagram,
        ushort messageId,
        byte[] token,
        CoapClientRequestOptions options,
        CancellationToken ct)
    {
        var ackTimeout = options.AckTimeout ?? TimeSpan.FromSeconds(2);
        var factor = options.AckRandomFactor ?? 1.5;
        var maxRetransmit = options.MaxRetransmit ?? 4;

        double lowMs = ackTimeout.TotalMilliseconds;
        double highMs = lowMs * factor;
        var firstRto = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * (highMs - lowMs) + lowMs);

        var currentRto = firstRto;

        for (int attempt=0; ; ++attempt)
        {
            ct.ThrowIfCancellationRequested();

            var msg = await ExchangeSingleAttemptAsync(
                udp,
                targetEndpoint,
                requestDatagram,
                messageId,
                token,
                currentRto,
                options.Timeout,
                options.ParseLimits,
                ct).ConfigureAwait(false);

            if (msg is not null)
                return msg;

            if (attempt == maxRetransmit)
            {
                throw new TimeoutException(
                    "CoAP Confirmable exchange timed out: no matching response after all retransmissions (RFC 7252, Section 4.2).");
            }

            currentRto += currentRto;
        }
    }

    private static bool EndPointEquals(IPEndPoint a, IPEndPoint b)
    {
        if (a.Port != b.Port)
            return false;

        return NormalizeIpForComparison(a.Address).Equals(NormalizeIpForComparison(b.Address));
    }

    /// <summary>
    /// Maps IPv4-mapped IPv6 addresses to IPv4 so a dual-stack <see cref="UdpClient"/> receive path
    /// matches a resolved <see cref="IPEndPoint"/> built from <see cref="AddressFamily.InterNetwork"/>.
    /// </summary>
    private static IPAddress NormalizeIpForComparison(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static Task SendUdpAsync(UdpClient udp, byte[] datagram, IPEndPoint endpoint, CancellationToken ct) =>
        udp.SendAsync(datagram.AsMemory(), endpoint, ct).AsTask();

    private static void EnforceMaxResponse(long currentBytes, int? max)
    {
        if (max.HasValue && currentBytes > max.Value)
        {
            throw new InvalidOperationException(
                $"Reassembled Block2 response exceeds MaxResponseBytes ({max.Value} bytes).");
        }
    }

    private static ushort NewMessageId() => (ushort)Random.Shared.Next(0, 0x10000);

    private static byte[] NewToken() => [(byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256)];
}
