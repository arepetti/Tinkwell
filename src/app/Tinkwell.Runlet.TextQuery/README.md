# Tinkwell.Runlet.TextQuery

Headless runlet that polls text-based devices and programs over TCP, serial, file, or shell command.
It applies regex to the response, scales the captured number, and writes values into Tinkwell measures via the measures gRPC client.

## Architecture

Implements `IRunlet`.
`TextQueryRunlet` registers options and a single hosted `TextQueryPollingManager`.
The manager loads `query` blocks with `TextQueryConfigParser` and discovers the measures service through `IServiceDiscovery`.
It runs one long-polling loop per configured source in parallel.
Each source gets an `ITextTransport` implementation.
`QueryAsync` returns text that is matched with a compiled regex.
Successful captures are written with `MeasuresGrpc.Measures.MeasuresClient.UpdateAsync`, passing `MeasuresGrpc.UpdateMeasureRequest`.
Those types come from generated code under `Tinkwell.Runlet.Measures.Grpc.V1`, which `TextQueryPollingManager` imports as `using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1`.

For how this runlet fits the coordinator and runner model, see the [Runlets catalog](../../docs/architecture/runlets.md#text-query).

## Key types

- `TextQueryRunlet` — `IRunlet` entry point; binds `TextQueryRunletOptions` and `TextQueryPollingManager`.
- `TextQueryPollingManager` — `BackgroundService` that parses config, resolves `MeasuresGrpc.Measures.MeasuresClient` from discovery, creates transports, and polls each `TextQueryReadDefinition` on an interval. Writes values with `UpdateAsync`.
- `ITextTransport` — `ConnectAsync` plus `QueryAsync` (send optional command with line terminator, read response with timeout); implemented for each transport kind.
- `TcpTextTransport` — `TcpClient` / `NetworkStream`; writes ASCII command plus terminator, reads until the terminator appears (or size/timeout limits).
- `SerialTextTransport` — `System.IO.Ports.SerialPort`; optional write, `ReadLine` with configurable read timeout.
- `FileTextTransport` — reads the configured file path each poll (ignores per-read send; full file content is the response).
- `CommandTextTransport` — runs the source-level shell command (`cmd /c` on Windows, `sh -c` elsewhere); stdout is the response (per-read `send` is not used).
- `TextQueryConfigParser` — extends `ConfigurationParser<TextQueryConfig>`; collects top-level `query` blocks into `TextQueryConfig` with `TextQuerySourceDefinition` and nested `read` → `TextQueryReadDefinition` (pattern, optional `send`, `group`, `scale`, `measure`).

## Configuration

Syntax, transports, and behavior are documented in the [Text Query reference](../../docs/reference/text-query.md).
The runlet `path` setting is summarized under [Runlets — `text-query`](../../docs/architecture/runlets.md#text-query).

## Dependencies and write path

- **Measures service** — required.
  The polling manager retries discovery for a short period.
  If the service never appears, polling stays disabled.

For outbound text over the same transport families (TCP, serial, file — not command), the actions runlet exposes the built-in `text-send` handler.
See the [`text-send` section in `Tinkwell.Runlet.Actions`](../Tinkwell.Runlet.Actions/README.md#text-send).
