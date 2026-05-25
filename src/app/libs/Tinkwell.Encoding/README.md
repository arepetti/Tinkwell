# Tinkwell.Encoding

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET application — no Tinkwell installation required.

Encoders and decoders for IoT payload formats: LwM2M TLV ([OMA-TS-LightweightM2M](https://www.openmobilealliance.org/release/LightweightM2M/), Section 6.4.3), SenML JSON ([RFC 8428](https://datatracker.ietf.org/doc/html/rfc8428)), and CoAP `text/plain` ([RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252), Section 12.3).

The library is a **set of codecs**, not a full LwM2M client and not a CoAP transport.
`PayloadCodec` is a thin convenience for the common case of decoding a single resource value from a CoAP response.

## Install

```bash
dotnet add package Tinkwell.Encoding
```

## Features

- Encode and decode LwM2M TLV records (Type-Length-Value)
- Encode and decode SenML JSON packs (RFC 8428), with RFC-conformant time resolution
- Content-format-aware codec dispatcher (`PayloadCodec`)
- Generic `PayloadValue` type for string, integer, float, boolean, opaque, time, and LwM2M object-link values

## Quick start

### Decode a TLV payload

```csharp
using Tinkwell.Encoding;

byte[] tlvBytes = /* from CoAP request payload */;
var records = TlvDecoder.Decode(tlvBytes);

foreach (var record in records)
{
    var value = TlvDecoder.Interpret(record.RawValue, PayloadType.Float);
    Console.WriteLine($"Resource {record.Identifier}: {value.AsDouble()}");
}
```

### Encode a TLV payload

```csharp
var records = new List<TlvRecord>
{
    new(TlvRecordType.Resource, 5700, PayloadValue.FromFloat(23.5), PayloadType.Float),
    new(TlvRecordType.Resource, 5701, PayloadValue.FromString("Cel"), PayloadType.String),
};

byte[] tlv = TlvEncoder.Encode(records);
```

### Nested TLV records

`TlvDecoder.Decode` performs a flat scan; `ObjectInstance` and `MultipleResource` records carry a sequence of inner TLV records as their `RawValue`.
Recurse manually:

```csharp
foreach (var outer in TlvDecoder.Decode(tlvBytes))
{
    if (outer.Type is TlvRecordType.ObjectInstance or TlvRecordType.MultipleResource)
    {
        var inner = TlvDecoder.Decode(outer.RawValue);
        // ... interpret inner records ...
    }
    else
    {
        var v = TlvDecoder.Interpret(outer.RawValue, PayloadType.Float);
    }
}
```

### Decode a SenML JSON payload

```csharp
string json = await response.Content.ReadAsStringAsync();
//   [{"bn":"/3303/0/","n":"5700","v":23.5}, {"n":"5701","vs":"Cel"}]

var records = SenmlJsonCodec.Decode(json);

foreach (var record in records)
{
    // record.Name is fully resolved (base-name `bn` already prepended).
    Console.WriteLine($"{record.Name}: {record.Value}");
}
```

A SenML pack is an array of records sharing a common base name (`bn`); each subsequent record's resolved name is `bn + n`.
Both `string` and `ReadOnlySpan<byte>` overloads are available — prefer the byte overload when you already have raw network bytes.

### Encode a SenML JSON pack

```csharp
var records = new List<SenmlRecord>
{
    new(5700, PayloadValue.FromFloat(23.5)),
    new(5701, PayloadValue.FromString("Cel")),
};

byte[] json = SenmlJsonCodec.Encode(objectId: 3303, instanceId: 0, records);
// → [{"bn":"/3303/0/","n":"5700","v":23.5},{"n":"5701","vs":"Cel"}]
```

`Encode` always returns UTF-8 bytes; if you need a `string`, use `System.Text.Encoding.UTF8.GetString(json)`.

### Reading values: pick the matching accessor

`PayloadValue` is strongly typed — calling the wrong `As*` accessor throws `InvalidOperationException`.
Inspect `Type` first when in doubt:

```csharp
var v = records[0].Value;

double d = v.Type switch
{
    PayloadType.Integer    => v.AsLong(),       // exact for whole-number JSON
    PayloadType.Float      => v.AsDouble(),
    PayloadType.Boolean    => v.AsBoolean() ? 1 : 0,
    _                      => throw new InvalidOperationException($"Unexpected: {v.Type}"),
};
```

`AsString()` is the only **total** accessor — it returns a printable representation for any `PayloadValue`, including `Empty`.

### SenML value typing rule

JSON has no type tag for numbers, so on decode:

- A whole-number literal that fits in `long` (e.g. `42`, `1700000000`) decodes as `PayloadType.Integer`.
- Anything else (`23.5`, `1e3`, fractions, out-of-range integers) decodes as `PayloadType.Float`.

A resource encoded from `PayloadType.Time` is written to `v` as Unix seconds and therefore decodes as `Integer`.
Use `PayloadValue.AsTime()` to recover a `DateTimeOffset`:

```csharp
var record = SenmlJsonCodec.Decode(json)[0];
DateTimeOffset when = record.Value.AsTime();   // works for Integer (Unix seconds) and Time
```

### SenML relative timestamps

RFC 8428 §4.5.3 lets records use timestamps relative to "now" when the value is below 2<sup>28</sup>.
For deterministic decoding (especially in tests) pass an explicit reference time:

```csharp
var fixedNow = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
var decoded = SenmlJsonCodec.Decode(json, fixedNow);

// String overloads are also available:
var decoded2 = SenmlJsonCodec.Decode(@"[{""n"":""5700"",""v"":1.0,""t"":-10}]", fixedNow);
```

### LwM2M object links

`PayloadType.ObjectLink` carries an `(objectId, instanceId)` pair (OMA "Objlnk").
On the SenML wire it appears as a string `"obj:inst"` in a `vlo` field; on TLV it is four big-endian bytes.

```csharp
var link = new ObjectLink(3303, 0);
var record = new SenmlRecord(5560, PayloadValue.FromObjectLink(link));
byte[] json = SenmlJsonCodec.Encode(3300, 0, [record]);
// → [{"bn":"/3300/0/","n":"5560","vlo":"3303:0"}]

var decoded = SenmlJsonCodec.Decode(json);
ObjectLink back = decoded[0].Value.AsObjectLink();
```

### Automatic format dispatch

```csharp
using Tinkwell.Coap;

CoapContentFormat contentFormat = response.ContentFormat ?? CoapContentFormat.TextPlain;

if (!PayloadCodec.IsSupported(contentFormat))
    throw new NotSupportedException($"Cannot decode {contentFormat}");

var value = PayloadCodec.DecodeSingleResource(
    payload, contentFormat, PayloadType.Float);
```

`PayloadCodec.DecodeSingleResource` returns the **first** record's value for TLV and SenML payloads.
The `expectedType` parameter is only consulted for `text/plain` (it picks the parsing rule) and `application/vnd.oma.lwm2m+tlv` (it interprets the first record's bytes); for SenML the type comes from the JSON value field, and for `application/octet-stream` the result is always `PayloadType.Opaque`.
An overload accepting `DateTimeOffset? now` is available for testable SenML relative-time decoding.
Use `PayloadCodec.IsSupported` if you build your own dispatch on top.

## Behavior notes

- **Unknown SenML fields** (`bu`, `bver`, `s`, `ut`, …) are silently ignored on decode.
  Only the standard `bn`/`bt`/`n`/`t`/`v`/`vs`/`vb`/`vd` and the LwM2M `vlo` extension are interpreted.
- **Time fidelity in SenML**: SenML JSON does not carry the original `PayloadType` on the wire.
  Encoding a `Time` resource writes Unix seconds as a numeric `v`; the decoder restores it as `Integer` (use `AsTime()` to bridge).
- **`PayloadType.None`**: a decoder produces `PayloadValue.Empty` (type `None`) when a record has no value field at all (RFC 8428 §4.4 allows name-only records) and when `PayloadCodec.DecodeSingleResource` is called on an empty payload.
  Treat it as "absent" — no `As*` accessor is meaningful except `AsString()`.
- **TLV nested decoding** is not automatic — see the nested-records example above.
  Recurse into `outer.RawValue` whenever `outer.Type` is `ObjectInstance` or `MultipleResource`; for any other type, call `TlvDecoder.Interpret(outer.RawValue, expectedType)`.
- **Allocations**: encoders return freshly allocated `byte[]`s.
  The TLV decoder copies each record's value bytes into a new array.
  The SenML decoder uses `Utf8JsonReader` and does not materialize a JSON DOM.

## Key types

| Type | Description |
|------|-------------|
| `PayloadValue` | A typed value (string, int, float, bool, opaque, time, object-link) with `From*` factories and `As*` accessors |
| `PayloadType` | Enum of supported value types |
| `ObjectLink` | LwM2M object link (object/instance ID pair, OMA "Objlnk") |
| `TlvEncoder` / `TlvDecoder` | LwM2M TLV encoding/decoding |
| `TlvRecord` / `DecodedTlvRecord` | A TLV record before encoding / after decoding |
| `SenmlJsonCodec` | SenML JSON encoding/decoding |
| `SenmlRecord` / `DecodedSenmlRecord` | A SenML record before encoding / after decoding |
| `PayloadCodec` | Content-format dispatcher for single-resource decoding |

## Specifications

- LwM2M TLV: OMA-TS-LightweightM2M_Core-V1_1, Section 6.4.3
- SenML JSON: [RFC 8428](https://datatracker.ietf.org/doc/html/rfc8428)
- CoAP content formats: [RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252), Section 12.3

## Dependency diagram

```
Tinkwell.Encoding
  └── Tinkwell.Coap  (for CoapContentFormat constants)
```
