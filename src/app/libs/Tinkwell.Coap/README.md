# Tinkwell.Coap

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used in any .NET application; no other Tinkwell packages are required.

**Low-level CoAP for .NET** — encode and decode messages ([RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252)), and send client requests over UDP with blockwise transfers ([RFC 7959](https://datatracker.ietf.org/doc/html/rfc7959)) and Confirmable retransmission ([RFC 7252, Section 4.2](https://datatracker.ietf.org/doc/html/rfc7252#section-4.2)).
**Zero external dependencies.**

## Who this is for

C# developers who need to talk to IoT devices or services that expose **CoAP** (Constrained Application Protocol).
You do **not** need to know the spec up front: the client API mirrors HTTP-style URIs and request/response, and the library handles large bodies with Block1/Block2 automatically.

## Install

```bash
dotnet add package Tinkwell.Coap
```

## First 15 minutes: GET a resource

CoAP uses a `coap://` URI (plain UDP, default port **5683**).
You describe the call with `CoapClientRequest`, transport settings with `CoapClientRequestOptions`, and send with **`CoapClient.SendAsync`** (the only public send entry point).

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Tinkwell.Coap;

static async Task Main()
{
    var uri = new Uri("coap://coap.me/hello");
    var request = new CoapClientRequest
    {
        Method = CoapMethod.Get,
        Accept = CoapContentFormat.TextPlain,
    };

    var response = await CoapClient.SendAsync(
        uri,
        request,
        CoapClientRequestOptions.Default,
        CancellationToken.None);

    if (response.Code == CoapCode.Content)
        Console.WriteLine(response.PayloadString);
    else
        Console.WriteLine($"Unexpected code: {CoapCode.ToDisplayString(response.Code)}");
}
```

`coap.me` is a public test server; swap the host and path for your device.
For **GET**, many devices return `text/plain`, `application/senml+json`, or `application/json`; set `Accept` to match the datasheet, or leave it unset and read `response.RequestContentFormat` / decode `response.Payload` according to what the device sends.

**Payloads:** `CoapMessage.PayloadString` is UTF-8 text only.
Sensors often return **binary** bodies (LwM2M TLV, CBOR, octet-stream).
Use `response.Payload` (bytes) and `response.RequestContentFormat` instead of assuming text.

See **Handling errors** below for timeouts and cancellations.
Hostnames ending in **`.local`** rely on mDNS; resolution is OS-dependent and can be flaky on Windows unless Bonjour/mDNS is set up—prefer the device IP when troubleshooting.

## POST with a JSON body

The string constructor `new CoapClientRequest("...")` defaults **Content-Format to `TextPlain`**.
If the body is JSON, pass **`CoapContentFormat.ApplicationJson`** explicitly (or the LwM2M media type your device expects).

```csharp
var uri = new Uri("coap://device.local/api/config");
var request = new CoapClientRequest("{\"enabled\":true}", CoapContentFormat.ApplicationJson)
{
    Method = CoapMethod.Post,
    Accept = CoapContentFormat.ApplicationJson,
};

var response = await CoapClient.SendAsync(
    uri,
    request,
    new CoapClientRequestOptions { Timeout = TimeSpan.FromSeconds(5) },
    CancellationToken.None);

// POST/PUT success is often 2.04 Changed or 2.01 Created — not necessarily 2.05 Content.
if (response.Code is CoapCode.Changed or CoapCode.Created or CoapCode.Content)
{
    // handle body if present
}
```

## Working with LwM2M devices (sidebar)

This library is **not** an LwM2M stack—only generic CoAP—but many gadgets speak CoAP using [OMA LwM2M](https://omaspecworks.org/release/lwm2m/) object/resource IDs.

- **URI path:** Vendor docs like “3303 / 0 / 5700” map to a CoAP path **`/3303/0/5700`** (object / instance / resource).
  Use that as the path in `coap://host/3303/0/5700` or `SendAsync`’s `path` argument.
- **Content-Format:** LwM2M servers often use **`CoapContentFormat.ApplicationLwm2mJson`** or **`ApplicationLwm2mTlv`**, not plain `ApplicationJson`.
  Match the registration or device spec; wrong `Accept` / Content-Format commonly yields `4.06 Not Acceptable` or empty bodies.
- **Reading values:** Many resources return TLV or SenML, not UTF-8 strings—decode **`response.Payload`** with an LwM2M or SenML library, not `PayloadString` alone.

For protocol details, see the LwM2M and SenML specifications; this package only carries the numeric content-format constants.

## Feature overview

| Included | Notes |
|----------|--------|
| Message **parse** / **build** | `CoapMessage.Parse`, `BuildRequest`, `BuildResponse` |
| **Client** over UDP | `CoapClient.SendAsync` — Confirmable requests, matches responses by token (and MID for ACKs) |
| **Block1 + Block2** | Transparent upload/download when payloads exceed one datagram ([RFC 7959](https://datatracker.ietf.org/doc/html/rfc7959)) |
| **CON retransmission** | [RFC 7252, Section 4.2](https://datatracker.ietf.org/doc/html/rfc7252#section-4.2) via `AckTimeout`, `AckRandomFactor`, `MaxRetransmit` on `CoapClientRequestOptions` |
| **DNS** | IPv4-first ordering with IPv6 fallback when a host resolves to several addresses |
| **Timeouts** | Per-receive ceiling (`Timeout`), optional overall deadline (`TotalTimeout`), plus retransmission limits |
| **Options & codes** | `CoapOption`, `CoapOptionNumber`, `CoapCode`, `CoapContentFormat`, `CoapBlockOption`, path patterns (`CoapPathMatcher`) |

**Not included (non-goals):** Observe subscriptions ([RFC 7641](https://datatracker.ietf.org/doc/html/rfc7641)) — option values can appear in parsed messages, but there is no observe client.
**Non-confirmable (NON) requests** are not exposed by `CoapClient` in this release: every send uses Confirmable (CON) and the RFC 7252, Section 4.2 retransmission machinery; NON support is planned for a later version.
No **DTLS** (no `coaps://`), no multicast discovery, **no server/listener mode**.
This is a client-focused, UDP, low-level toolkit.
The **`coap://` scheme is not validated**—the client always uses **plain UDP**; there is no TLS or `coaps` transport here.

## Handling errors

- **`TimeoutException`** — no matching response after all Confirmable retransmissions (RFC 7252, Section 4.2).
- **`OperationCanceledException`** — your `CancellationToken`, or `TotalTimeout` on options.
  *`TaskCanceledException` (for example a token already canceled at call time) is a subclass and is included.*
- **`SocketException`** — DNS/network issues, or no addresses for the host.
  On **Windows**, ICMP “port unreachable” (nothing listening on the UDP port) sometimes surfaces as `SocketException` during **receive** instead of a timeout.
- **`InvalidOperationException`** — blockwise protocol failures, such as a reassembled Block2 response larger than `MaxResponseBytes`, or a follow-up response that's missing Block2 or carries a mismatched block number / size exponent (RFC 7959, Section 2.4).
- **`FormatException`** — invalid datagram in `CoapMessage.Parse` (not used for unrelated UDP noise in `CoapClient`, which discards non-matching datagrams).

IntelliSense and `<remarks>` on `CoapClient` and `CoapClientRequestOptions` spell out correlation, blockwise behavior, and defaults.

## Core types (cheat sheet)

| Type | Role |
|------|------|
| `CoapClient` | `SendAsync` overloads (URI or host/path/port) |
| `CoapClientRequest` | Method, payload, content format, accept, optional token/message id (defensive copy on payload/token) |
| `CoapClientRequestOptions` | Immutable; **`Default`** has the same property values as **`new CoapClientRequestOptions()`**—use `Default` to reuse one shared instance, or `new()` when you prefer a fresh object reference |
| `CoapMessage` | Parsed or built message; `UriPath`, `UriQuery`, `Block1`/`Block2`, `Payload` / `PayloadString` |
| `CoapBlockOption` | Blockwise NUM/M/SZX; `FromOption` / `ToUInt` for wire format |
| `CoapOption` / `CoapOptionNumber` | Raw options and well-known numbers |
| `CoapCode` / `CoapMethod` | Wire codes and helpers like `ToDisplayString` |
| `CoapContentFormat` | Common IANA content-format ids |
| `CoapPathMatcher` | `+` and `#` wildcard path matching |
| `CoapConstants` | Parser/builder wire constants (includes Observe-related masks and the hard parse ceilings) |
| `CoapMessageParseLimits` | Configurable bounds (max message size, option count, option value length) applied by `CoapMessage.Parse` to reject pathological datagrams. On the client, set them via `CoapClientRequestOptions.ParseLimits`; on the server, via `CoapServerOptions.ParseLimits` |

## Advanced topics

- **Blockwise** — Outgoing Block1 is used when the payload is larger than `RequestBlockSize` (default 1024 bytes), or when `ForceBlockwise` is true.
  Set `RequestBlockSize` to `null` to disable Block1.
  Block2 reassembly is automatic when the server sets the “more” bit.
- **Retransmission & timeouts** — Tune `AckTimeout`, `AckRandomFactor`, `MaxRetransmit` for lossy links.
  `Timeout` caps individual receive waits within one attempt; it does not extend past the Section 4.2 timer for that attempt.
- **Raw messages** — Use `CoapMessage.BuildRequest` / `BuildResponse` and your own `UdpClient` if you need full control; use `Parse` on incoming datagrams.

## Parse and respond (no client)

If you only need the codec (for example in a gateway):

```csharp
// Apply caps if the datagram comes from an untrusted peer; the default
// CoapMessage.Parse(datagram) overload uses CoapMessageParseLimits.Default.
var message = CoapMessage.Parse(datagramBytes, CoapMessageParseLimits.Default);

var reply = CoapMessage.BuildResponse(
    CoapMessageType.Acknowledgement,
    CoapCode.Content,
    messageId: message.MessageId,
    token: message.Token,
    contentFormat: CoapContentFormat.TextPlain,
    payload: System.Text.Encoding.UTF8.GetBytes("ok"));
```

Both `BuildRequest` and `BuildResponse` accept an optional `extraOptions: IEnumerable<CoapOption>?` for options the named parameters don't expose (for example `ETag`, `Max-Age`, `If-Match`, `Location-Path`).
Extras are interleaved with the named options by ascending option number, and the original input order is preserved among entries that share the same number (stable sort), so a request carrying multiple `ETag`s reaches the wire in the order you wrote them.
`CoapOptionNumber` carries constants for the options this library decodes itself; for everything else use the IANA-assigned number directly.

```csharp
// Attach a Location-Path=/things/42 hint to a 2.01 Created response after a POST.
var reply = CoapMessage.BuildResponse(
    CoapMessageType.Acknowledgement,
    CoapCode.Created,
    messageId: message.MessageId,
    token: message.Token,
    contentFormat: null,
    payload: null,
    extraOptions: new[]
    {
        new CoapOption(CoapOptionNumber.LocationPath, "things"u8.ToArray()),
        new CoapOption(CoapOptionNumber.LocationPath, "42"u8.ToArray()),
    });
```

## RFC references

- [RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252) — CoAP
- [RFC 7959](https://datatracker.ietf.org/doc/html/rfc7959) — Block-wise transfers
- [RFC 7641](https://datatracker.ietf.org/doc/html/rfc7641) — Observe (parsing/building only in this library; no observe client)

## License

See the repository license.
This package is maintained as part of Tinkwell but is usable standalone.
