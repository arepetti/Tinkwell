# Tinkwell.Cli.Sdk

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This is an SDK package for building Tinkwell extensions — it assumes Tinkwell is installed as the host application.

SDK for building Tinkwell CLI command extensions.
Third-party runlets can ship their own `tw` commands by creating a class library that references this package.

## Naming convention

Command DLLs must follow the pattern:

```
Tinkwell.Cli.Commands.{Domain}[.{Platform}].dll
```

- **Domain** (required) — the feature area, e.g. `Mqtt`, `Coap`, `Lwm2m`
- **Platform** (optional) — `Windows`, `Linux`, or `MacOS`.
  When present, the DLL is loaded only on the matching OS.

Valid examples:
- `Tinkwell.Cli.Commands.Mqtt.dll` — loaded on all platforms
- `Tinkwell.Cli.Commands.Mqtt.Windows.dll` — loaded only on Windows
- `Tinkwell.Cli.Commands.Coap.Linux.dll` — loaded only on Linux

Invalid (ignored by the loader):
- `Tinkwell.Cli.Commands.dll` — no domain
- `Tinkwell.Cli.Commands.Windows.dll` — platform without domain

## Quick start

1. Create a class library targeting `net10.0`.
2. Reference `Tinkwell.Cli.Sdk`.
3. Declare the branch at assembly level:

```csharp
using Tinkwell.Cli.Commands;

[assembly: CliBranch("mybranch", "My custom commands")]
```

4. Create a command class:

```csharp
using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

[CliCommand("mybranch", "hello", Description = "Say hello")]
public sealed class HelloCommand : AsyncCommand<TwSettings>
{
    public override Task<int> ExecuteAsync(
        CommandContext context, TwSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);
        output.WriteSuccess("Hello from my extension!");
        return Task.FromResult(0);
    }
}
```

5. Build the DLL and place it next to `tw.exe`:

```
tw mybranch hello
```

## Available types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `CliCommandAttribute` | `Tinkwell.Cli.Commands` | Marks a command for discovery |
| `CliBranchAttribute` | `Tinkwell.Cli.Commands` | Declares a branch with description (assembly-level) |
| `TwSettings` | `Tinkwell.Cli` | Base settings with pipe, format, verbose options |
| `OutputContext` | `Tinkwell.Cli` | Table / list / JSONL output rendering |
| `ColumnDef<T>` | `Tinkwell.Cli` | Column definition for `OutputContext.WriteTable` |
| `OutputFormat` | `Tinkwell.Cli` | Enum: Table, List, Jsonl |
| `PipeCommandRunner` | `Tinkwell.Cli` | Send commands to the coordinator pipe |
| `PipeResult` | `Tinkwell.Cli` | Parsed coordinator response |
| `TwCommandException` | `Tinkwell.Cli` | Exception for command failures |
