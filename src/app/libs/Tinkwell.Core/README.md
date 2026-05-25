# Tinkwell.Core

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> Shared infrastructure: named-pipe I/O, logging, environment paths, and TLS options.
> This library is referenced by the full Tinkwell host and is safe to use from standalone .NET code that only needs these primitives (for example, custom tools that talk to the coordinator’s pipe server).

## Quick start

Add the package to a .NET 10 project (version matches your Tinkwell release or feed):

```xml
<PackageReference Include="Tinkwell.Core" Version="0.5.0" />
```

Use the line-oriented named-pipe client by deriving from `PipeClient`, or the built-in `PipeServer` for a long-lived listener.
For logging, use `ILoggingBuilder` extensions from `Tinkwell.Logging` (for example `AddTinkwellConsole()` for the compact Tinkwell console format; you can also pass `b => b.AddTinkwellConsole()` to `LoggerFactory.Create`).

## API overview

| Area | Key types | Role |
|------|-----------|------|
| **Named pipes** | `PipeServer`, `PipeClient`, `PipeConnection` | One JSONL line per request/response over a `NamedPipeServerStream` / `NamedPipeClientStream`. |
| **Logging** | `TinkwellConsoleFormatter` | Compact `HH:mm:ss.fff` prefix and category formatting. |
| **Environment** | `TinkwellEnvironment` | Resolves data and working directory conventions for Tinkwell processes. |
| **IDs** | `ShortIdGenerator` | Short hex identifiers (SHA-256 of a GUID, truncated) for runtimes that need them. |
| **TLS (client/server)** | `TlsOptions`, `TlsMode` | Optional TLS for gRPC: bound from the `Tls` section of the runner’s `appsettings.json` (Kestrel uses `CertificatePath` when enabled). |
| **Plugins** | `PluginCatalog`, `PluginResolver` | When present, the runner loads `*.twpkg` plugin entries for dependency resolution. |
| **Text** | `CommandLineTokenizer` | Tokenizes pipe command lines (used by the coordinator and runner IPC). |
| **Exceptions** | `TinkwellException` | Base for Tinkwell-specific failures. |

## Configuration: TLS (`Tls` section)

Used by gRPC runners and related clients.
Bind `TlsOptions` from configuration:

| Property | Meaning |
|----------|---------|
| `Mode` | `None` (HTTP/2 cleartext), `SelfSigned` (permissive for dev), or `Standard` (full validation). |
| `CertificatePath` | Path to a `.pfx` when TLS is active (Kestrel). |

`TlsOptions.Scheme` and `IsEnabled` are derived from `Mode` and are useful when building service URLs (for example in service discovery).
