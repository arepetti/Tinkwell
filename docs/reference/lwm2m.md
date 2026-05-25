# LwM2M support

Tinkwell supports OMA Lightweight M2M (LwM2M) for device management and data ingestion from constrained IoT devices.
LwM2M runs over CoAP (RFC 7252) and provides a standardized object/resource model for sensors and actuators.

## Architecture

The implementation is split across three projects:

| Project | Role |
|---|---|
| **Tinkwell.Lwm2m** | Standalone library: LwM2M object/resource model, `Lwm2mPath`, curated IPSO registry, registration and link-format types (query + payload). **No payload codecs** — TLV, SenML-JSON, and text/plain are in **Tinkwell.Encoding** |
| **Tinkwell.Lwm2m.Server** | LwM2M server implementation |
| **Tinkwell.Runlet.Lwm2m** | Headless runlet: CoAP server with registration, read/write dispatch, resource store |

Additionally, **CoAP Observe** (RFC 7641) was added to `Tinkwell.Runlet.Coap` as a prerequisite for LwM2M observation notifications.

## Quick start

```tw
runner integrations from "Tinkwell.Runner.Headless.dll" {
    runlet lwm2m from "Tinkwell.Runlet.Lwm2m.dll";
}

lwm2m sensors {
    port = 5684

    registration config {
        default-lifetime = 86400
        emit-events = true
    }

    object "3303" {
        resource "5700" {
            measure = "temperature"
            observable = true
        }
    }

    object "3304" {
        resource "5700" {
            measure = "humidity"
        }
    }
}
```

This starts an LwM2M server on UDP port 5684 that accepts device registrations and maps IPSO Temperature (3303) and Humidity (3304) sensor values to Tinkwell measures.

## Supported operations

| Operation | Method | Path | Description |
|---|---|---|---|
| Register | POST | `/rd` | Client registers with endpoint name and object list |
| Update | POST | `/rd/{location}` | Client refreshes registration lifetime |
| Deregister | DELETE | `/rd/{location}` | Client leaves the server |
| Read | GET | `/{obj}/{inst}/{res}` | Read a resource value |
| Read (instance) | GET | `/{obj}/{inst}` | Read all resources for an instance |
| Write | PUT/POST | `/{obj}/{inst}/{res}` | Write a resource value |

## Supported encodings

These are implemented in **Tinkwell.Encoding** and used by the LwM2M runlet (and by other Tinkwell components).
They are **not** part of the **Tinkwell.Lwm2m** package, which is types and metadata only.

| Format | Content-Format ID | Read | Write |
|---|---|---|---|
| Text/plain | 0 | Yes | Yes |
| LwM2M TLV | 11542 | Yes | Yes |
| SenML-JSON | 110 | Yes | Yes |

## Lightweight alternative

If your devices send data to well-known LwM2M URIs using plain text and you don't need registration semantics, you can use standard CoAP bindings instead of the full LwM2M runlet.
See the [how-to guide](../user-guide/how-to.md#receive-lwm2m-style-data-without-the-lwm2m-runlet) for an example.

## Deferred features

The following are tracked in the [roadmap](../contributing/roadmap.md) for future work:

- Bootstrap (object /0, `/bs` endpoint)
- Execute operation (per-resource handler callbacks)
- Firmware Update (object /5)
- Access Control (object /2)
- Block1/Block2 transfer for large payloads
- DTLS transport security
- SenML-CBOR encoding
- Composite read/write
