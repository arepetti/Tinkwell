# Published Libraries

All libraries under `src/app/libs/` are published to NuGet.
Each has its own `README.md` with API details and usage examples.

## Standalone libraries

These can be used independently in any .NET application — no Tinkwell installation required.

| Library | Description |
|---------|-------------|
| `Tinkwell.Core` | Shared infrastructure: named pipes, logging, environment, TLS |
| `Tinkwell.Package` | Secure package format: pack, unpack, verify, sign |
| `Tinkwell.Coap` | CoAP protocol implementation |
| `Tinkwell.Coap.Server` | CoAP server with Observe support |
| `Tinkwell.Lwm2m` | LwM2M protocol types and encoding (TLV, SenML) |
| `Tinkwell.Lwm2m.Server` | LwM2M server with registration and object management |
| `Tinkwell.Modbus` | Modbus RTU/TCP client: read/write registers with typed decoding |
| `Tinkwell.Encoding` | Binary encoding utilities |
| `Tinkwell.Expressions` | Expression evaluation engine |
| `Tinkwell.Configuration.Parser` | `.tw` grammar parser |
| `Tinkwell.Configuration.Abstractions` | Configuration model contracts |

## Global tools

| Tool | Description |
|------|-------------|
| `Tinkwell.Build.Ci` | CI packaging tool (`tinkwell-ci-package`): creates `.twpkg` files from a flat directory without requiring the full Tinkwell CLI |

## SDK packages

These are for building Tinkwell extensions and assume Tinkwell is installed as the host application.

| Library | Description |
|---------|-------------|
| `Tinkwell.Actions.Abstractions` | Action execution contracts for the actions subsystem |
| `Tinkwell.Cli.Sdk` | SDK for building `tw` command extensions |
| `Tinkwell.Events.Abstractions` | Event bus and envelope contracts for the events subsystem |
| `Tinkwell.Integration.Abstractions` | Integration binding contracts |
| `Tinkwell.Runner.Abstractions` | Runner and runlet contracts (`IRunlet`, `IGrpcRunlet`) |
| `Tinkwell.Runlet.Mqtt.Abstractions` | MQTT runlet extension points |
| `Tinkwell.Runlet.Coap.Abstractions` | CoAP runlet extension points |
| `Tinkwell.Runlet.ProtobufGateway.Abstractions` | Protobuf gateway contracts |
| `Tinkwell.Telemetry` | OpenTelemetry integration |
