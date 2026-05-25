# Tinkwell Studio

> **Status:** experimental. Not part of any official Tinkwell release.
> **Platforms:** Windows 10 1809+ / Windows 11 (x64, ARM64). Windows 11 is recommended for Mica.
> **Requires:** `tw` on the PATH (or an explicit path in Studio settings).

Tinkwell Studio is a Windows desktop GUI for monitoring and testing a local
Tinkwell runtime. It is a thin wrapper around the `tw` CLI: every action maps
to one of the CLI's commands invoked with `--format jsonl --non-interactive`.
The only exception is MQTT *subscribe*, which is handled in-process with
MQTTnet because the CLI does not offer a subscribe/watch command.

## Why

The CLI already implements all the logic (pipe + gRPC transport, service
discovery, schemas, validation). Studio avoids duplicating any of that by
treating the CLI's JSONL output as its data source and the CLI's exit codes +
`stderr` as its error channel.

## Design

- **Fluent Design**: Windows 11 Mica backdrop (with an Acrylic fallback on
  Windows 10), extended title bar, `NavigationView` side rail, Segoe Fluent
  Icons, and built-in theme brushes (`CardBackgroundFillColorDefaultBrush`,
  `LayerFillColorDefaultBrush`, ...) so Studio automatically respects the
  system light/dark setting and accent color.
- **Two-project layout**: all logic lives in `Tinkwell.Studio.Core`, a pure
  .NET library with no UI framework references. `Tinkwell.Studio.WinUI` is
  the thin WinUI 3 view layer. Tests only depend on Core, so they run
  anywhere .NET runs.
- **Unpackaged WinUI 3 app**: ships as a plain `.exe` (self-contained
  Windows App SDK), consistent with the rest of Tinkwell. No MSIX / Store.

## Categories

| Category | Primary commands |
|----------|------------------|
| Home / Status | `tw status`, `tw ping`, `tw info` |
| Runners | `tw runners list`, `tw runners health` |
| Services | `tw services list`, `tw services find <family>` |
| Store | `tw store list` / `watch` / `get` / `set` / `delete` |
| Measures | `tw measures list` / `watch` / `get` / `set` |
| Events | `tw events watch`, `tw events publish` |
| MQTT | MQTTnet subscribe (in-process) + `tw mqtt publish` / `ping` / `start-broker` |
| CoAP | `tw coap send` |

## Building

Requires the .NET 10 SDK. From `src/extras/studio`:

```pwsh
dotnet restore Tinkwell.Studio.slnx
dotnet build Tinkwell.Studio.slnx
```

Run:

```pwsh
dotnet run --project src/Tinkwell.Studio.WinUI/Tinkwell.Studio.WinUI.csproj
```

## Publishing

Windows x64, self-contained (includes the Windows App SDK runtime):

```pwsh
dotnet publish src/Tinkwell.Studio.WinUI/Tinkwell.Studio.WinUI.csproj `
  -c Release -r win-x64 --self-contained true
```

Windows ARM64:

```pwsh
dotnet publish src/Tinkwell.Studio.WinUI/Tinkwell.Studio.WinUI.csproj `
  -c Release -r win-arm64 --self-contained true
```

## Notes

- Studio uses the Windows App SDK (WinUI 3) and the Community Toolkit
  `DataGrid` — no FluentAvalonia / Material / Semi, intentionally minimal.
- MQTT credentials entered in the UI are held in memory only and are not
  persisted across runs.
- The exact JSONL shapes Studio consumes are pinned via golden-sample
  tests in `tests/Tinkwell.Studio.Tests/GoldenSamples/`; if the CLI output
  changes, those tests fail loudly.
