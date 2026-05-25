# Plugins

Tinkwell supports a plugin system that allows extending the platform with custom runlets, integration bindings, action handlers, CoAP middleware, and resource handlers — all loaded from external assemblies at runtime.

## Overview

A **plugin** is a set of one or more .NET assemblies stored in a versioned directory.
When Tinkwell starts, it scans well-known plugin directories, builds a catalog of all available plugins, and resolves assembly references on demand.
Plugins **resolved through the catalog** are loaded in isolated `AssemblyLoadContext` instances, which prevents dependency conflicts while sharing core Tinkwell types with the host.
Assemblies that are loaded by a direct file path, or that fall back to the default context because the catalog cannot resolve them, share the host's `AssemblyLoadContext` — see "[Non-catalog loads](#non-catalog-loads)" below.

Plugins can provide:

- **Runlets** — custom `IRunlet` implementations loaded via `from "My.Runlet.dll"`
- **Integration bindings** — `IIntegrationBinding` implementations for MQTT/CoAP value processing
- **Action handlers** — `IActionHandler` implementations for event-driven logic
- **CoAP middleware** — `ICoapRequestMiddleware` and `ICoapResourceHandler` implementations

## Plugin Source Directories

Tinkwell scans four directories for plugins, in order of **decreasing priority**:

| Priority | Source | Path | Purpose |
|----------|--------|------|---------|
| 1 (highest) | Environment variable | `TINKWELL_PLUGIN_PATH` (`;`-separated) | Explicit override for development and CI |
| 2 | User home | `~/Tinkwell/plugins/` | Convenient per-user install |
| 3 | User app data | `{LocalApplicationData}/Tinkwell/plugins/` | OS-conventional per-user data |
| 4 (lowest) | App-local | `{AppContext.BaseDirectory}/plugins/` | Ships with the application |

**Platform-specific paths for user app data (priority 3):**

- **Windows:** `%LOCALAPPDATA%\Tinkwell\plugins\`
- **Linux:** `~/.local/share/Tinkwell/plugins/`
- **macOS:** `~/Library/Application Support/Tinkwell/plugins/`

Non-existent directories are silently skipped.
The `TINKWELL_PLUGIN_PATH` variable supports multiple paths separated by `;`:

```
TINKWELL_PLUGIN_PATH=C:\dev\plugins;D:\shared\plugins
```

## Directory Layout

Each plugin occupies a directory named `{name}@{major}.{minor}.{patch}`:

```
plugins/
├── my-runlet-json@1.0.0/
│   ├── package.tw              (optional: full metadata)
│   ├── My.Runlet.Json.dll      (main assembly)
│   ├── My.Runlet.Json.deps.json (optional: dependency manifest)
│   └── SomeThirdParty.dll      (plugin-private dependency)
├── my-runlet-json@1.1.0/
│   ├── My.Runlet.Json.dll
│   └── SomeThirdParty.dll
└── sensor-binding@2.0.0/
    ├── package.tw
    └── Sensor.Binding.dll
```

### Naming rules

- The directory name **must** contain exactly one `@` separating the plugin name from the version.
  If `@` appears in the plugin name itself, the **last** `@` is used as the separator.
- The version must be parseable as a .NET `System.Version` (`major.minor.patch`).
- Directories that don't match this format are **skipped** with a warning in the log.
- Directories that contain no `.dll` files are **skipped**.

### `package.tw`

The optional `package.tw` file follows the standard [Tinkwell package format](packages.md) and can carry metadata.
It is not required for plugin discovery — the directory name is the primary index.

Known properties:

| Key | Description |
|-----|-------------|
| `name` | Package identifier (required) |
| `version` | Package version (semver) |
| `author` | Primary author name |
| `author-email` | Author contact email |
| `company` | Company or organization name |
| `company-website` | Company website URL |
| `company-email` | Company contact email |
| `support-email` | Support/help desk email |
| `description` | Short package description |
| `license` | License identifier (e.g. MIT, Apache-2.0) |
| `license-url` | URL to full license text |
| `copyright` | Copyright notice |
| `contributors` | Comma-separated contributor names |
| `project-website` | Project homepage URL |
| `documentation-website` | Documentation URL |
| `terms-url` | URL to Terms & Conditions |

Any other key-value pair is a custom property.
The `product-version` property below is one such convention used by plugins.

#### `product-version`

Plugins should include a `product-version` custom property in their `package.tw` to declare compatibility with the Tinkwell application.
The value uses [NuGet version range syntax](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#version-ranges):

| Notation | Meaning |
|----------|---------|
| `[0.1,)` | >= 0.1.0 (any version from 0.1.0 onwards) |
| `[0.1, 1.0)` | >= 0.1.0 and < 1.0.0 |
| `[1.0]` | Exactly 1.0.0 |
| `0.1.0` | >= 0.1.0 (minimum version, inclusive) |

Example:

```
package "my-plugin" {
  version = "1.0.0"
  product-version = "[0.1,)"
}
```

When installing via `tw plugin install`, the installer compares the range against the running `tw` version (derived from its `AssemblyInformationalVersion`).
If the current version falls outside the range, installation is rejected unless `--force` is specified.

### `.deps.json`

When present, `.deps.json` enables the `AssemblyDependencyResolver` for precise dependency resolution, including native libraries.
This file is automatically generated by `dotnet publish`.
Its presence is optional but recommended for plugins with complex dependency trees.

## Resolution Rules

### By assembly filename

When Tinkwell encounters a reference like `from "My.Runlet.Json.dll"`, it searches the catalog:

1. Collect all plugins from **all sources** that contain a file named `My.Runlet.Json.dll` (case-insensitive).
2. Pick the plugin with the **highest version**.
3. On version tie, the plugin from the **higher-priority source** wins.

### By plugin name

Plugins can also be resolved by name (e.g., `my-runlet-json`):

1. Collect all entries matching the plugin name (case-insensitive).
2. Apply an optional minimum version constraint.
3. Pick the highest version, with priority as tiebreaker.

### Multi-source merging

All source directories are scanned and their plugins are **merged** into a single catalog:

- Every unique `name@version` appears once — when the same `name@version` exists in multiple sources, the higher-priority source wins and the others are ignored.
- Different versions of the same plugin from different sources all appear in the catalog.
- Resolution always picks the **latest version** regardless of which source it came from.

**Example:** If `~/Tinkwell/plugins/` has `sensor@1.0.0` and `{BaseDirectory}/plugins/` has `sensor@2.0.0`, version `2.0.0` is chosen even though it comes from a lower-priority source.
Priority only matters as a tiebreaker when versions are identical.

## Shared Assembly Policy

Plugin assemblies **resolved through the plugin catalog** are loaded in isolated `AssemblyLoadContext` instances.
However, certain assemblies are **shared** with the host application to ensure type identity across boundaries.

### Two-tier detection

**Tier 1 — Runtime prefix fast-path:** Assemblies whose names start with `System.`, `Microsoft.`, or `netstandard` are always resolved from the runtime/host.
No disk check is performed.

**Tier 2 — Host directory probe:** If `{HostBaseDirectory}/{AssemblyName}.dll` exists on disk, the assembly is considered shared.
The plugin's own copy (if any) is never loaded.

This means the shared assembly set is **self-maintaining**: any DLL shipped alongside the Tinkwell host automatically becomes shared for all plugins.
There is no list to update.

### Implications

- All `Tinkwell.*.dll` assemblies are shared (they exist in the host directory).
- Third-party libraries bundled with the host (e.g., `MQTTnet.dll`, `Parlot.dll`) are also shared.
- Plugin-private dependencies (not present in the host directory) are loaded in the plugin's own context and are **not** visible to other plugins or the host.

## Version Compatibility

When a plugin assembly is loaded, Tinkwell checks its referenced `Tinkwell.*` assemblies against the host's loaded versions:

| Condition | Behavior |
|-----------|----------|
| Host version >= plugin reference (same major) | Normal operation |
| Host version < plugin reference | **Warning logged** — the plugin may use APIs not present in the host |
| Major version mismatch | **Warning logged** — breaking changes are likely |

Warnings are logged but loading proceeds.
This avoids blocking legitimate use cases while making version problems visible.

## Authoring a Plugin

### Project setup

Create a standard .NET class library targeting the same framework as the host (`net10.0`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- Reference the abstractions you need -->
    <PackageReference Include="Tinkwell.Runlet.Mqtt.Abstractions" Version="*" />
    <!-- Or for runlets: -->
    <PackageReference Include="Tinkwell.Runner.Abstractions" Version="*" />
  </ItemGroup>
</Project>
```

### Building

Use `dotnet publish` to produce a self-contained output with all dependencies:

```bash
dotnet publish -c Release -o publish/
```

### Packaging

Create the package directory structure and generate a `package.tw` manifest:

```bash
mkdir my-plugin-pkg
mkdir my-plugin-pkg/content
cp publish/* my-plugin-pkg/content/

tw package create-manifest my-plugin-pkg \
  --set name=my-plugin \
  --set version=1.0.0 \
  --set author="Your Name" \
  --set description="What this plugin does" \
  --set "product-version=[0.1,)"
```

You can also run `tw package create-manifest` without `--set` to be prompted interactively.
The resulting structure is:

```
my-plugin-pkg/
├── package.tw
└── content/
    ├── My.Plugin.dll
    ├── My.Plugin.deps.json
    └── SomePrivateDep.dll
```

Then pack and sign it:

```bash
tw identity generate-key publisher.key publisher.pub   # once, keep the private key safe
tw package pack my-plugin-pkg my-plugin-1.0.0.zip --key publisher.key
```

#### CI packaging (without the full CLI)

In CI pipelines where installing Tinkwell is impractical, use the `tinkwell-ci-package` global tool instead.
It takes a flat directory containing `package.tw` alongside DLLs (e.g. `dotnet publish` output) and produces a `.twpkg` directly:

```bash
dotnet tool install -g Tinkwell.Build.Ci
dotnet publish -c Release -o ./staging

tinkwell-ci-package pack ./staging -o my-plugin.twpkg --sign
# signing key is read from TW_SIGNING_KEY env var (base64-encoded PKCS#8)
```

See the [tool README](https://github.com/arepetti/Tinkwell/blob/main/src/app/libs/Tinkwell.Build.Ci/README.md) for full options and GitHub Actions examples.

### Installing

Use `tw plugin install` to verify and install a package in one step:

```bash
tw plugin install my-plugin-1.0.0.zip --key publisher.pub
```

This verifies the package signatures, reads the manifest, and extracts the content to `{LocalApplicationData}/Tinkwell/plugins/my-plugin@1.0.0/`.

To upgrade and remove older versions at the same time:

```bash
tw plugin install my-plugin-1.1.0.zip --key publisher.pub --update
```

### Managing plugins

```bash
# List all discovered plugins
tw plugin list

# Remove the latest version
tw plugin uninstall my-plugin

# Remove a specific version
tw plugin uninstall my-plugin@1.0.0

# Remove all versions
tw plugin uninstall my-plugin --all
```

See the [CLI reference](../user-guide/cli.md#plugin) for full details on all options.

### Manual installation (development)

During development you can skip packaging and copy files directly:

```bash
mkdir -p ~/Tinkwell/plugins/my-plugin@0.1.0
cp publish/* ~/Tinkwell/plugins/my-plugin@0.1.0/
```

This is not recommended for production — use signed packages for integrity verification.

### What to include

- **Your assembly** and any **private dependencies** not bundled with the host.
- **`.deps.json`** — recommended for accurate dependency resolution.
- **`package.tw`** — optional metadata for tools and documentation.

### What NOT to include

- **Tinkwell assemblies** (`Tinkwell.Core.dll`, `Tinkwell.Runner.Abstractions.dll`, etc.) — these are shared from the host.
  Including them wastes space and they will be ignored.
- **Runtime assemblies** (`System.*.dll`, `Microsoft.*.dll`) — same reason.
- **Host-bundled third-party libraries** — check the host's directory; if it's already there, don't include it.

## Quirks and Pitfalls

### Type identity across `AssemblyLoadContext` boundaries

Types from **shared assemblies** (loaded from the Default ALC) maintain identity across plugin boundaries.
This means:

- An `IRunlet` interface from the host is the same type in every plugin.
- Types defined in `Tinkwell.Core` can be passed between the host and any plugin.

However, types from **plugin-private assemblies** do NOT share identity:

- If Plugin A and Plugin B both bundle `Newtonsoft.Json.dll`, they each get their own copy.
  A `JObject` from Plugin A is a **different type** than a `JObject` from Plugin B.
- This is by design — it prevents version conflicts between plugins.

### Same DLL name as host

If a plugin ships a DLL with the same filename as one in the host directory, **the host's version always wins silently**.
The plugin's copy is never loaded.
This ensures the host's known-good version is used.

### Two plugins, same third-party library, different versions

Each plugin loads its own copy in its own `AssemblyLoadContext`.
This works correctly — both versions coexist without conflict.
The trade-off is slightly higher memory usage.

### Directory names without `@`

Directories in the plugins folder that don't match the `{name}@{version}` pattern are silently skipped with a warning in the log.
They won't cause errors.

### Non-catalog loads

Isolation is a property of the **catalog**, not of the plugin system as a whole.
The following load paths bypass the catalog and land the assembly in the host's default `AssemblyLoadContext`, alongside Tinkwell's own code:

- Runlets referenced by a path that contains a directory separator (e.g. `from "./bin/my-runlet.dll"`).
  The `AssemblyPath` is loaded directly from disk by the coordinator runlet loader.
- Action handlers named by a bare filename that is **not** discovered by the plugin catalog (handler assemblies dropped next to the host binary).
- Integration bindings (MQTT/CoAP) whose assembly name is not declared in any plugin directory.
  They are resolved via the default `AssemblyLoadContext` the same way the host itself resolves references.

Consequences:

- Type identity with the host is preserved unconditionally — these loads behave exactly like a reference that the host itself brought in at startup.
- There is **no** `AssemblyLoadContext`-level isolation between non-catalog assemblies and the host, or between two non-catalog assemblies.
  Dependency conflicts follow normal .NET first-one-wins rules.
- Version compatibility checks (see "[Version Compatibility](#version-compatibility)") are only performed for catalog-resolved plugins.

Recommendation: install third-party runlets, action handlers, and bindings as plugin packages via `tw plugin install` whenever isolation or side-by-side versioning matters.
The non-catalog load path exists so that first-party assemblies shipped next to the host, and ad-hoc paths during local development, continue to work; it is intentionally **not** a security boundary.

### Plugin loading is lazy

The plugin catalog scans directories at startup, but assemblies are not loaded until they are actually referenced.
This keeps startup fast even with many plugins installed.

### `AssemblyLoadContext` caching

Each catalog-resolved plugin directory gets exactly one `AssemblyLoadContext`, created on first use and cached for the lifetime of the process.
Multiple assemblies from the same plugin directory share a context.
Non-catalog loads use the default `AssemblyLoadContext` instead; see "[Non-catalog loads](#non-catalog-loads)".

## Security

Plugins run with **full trust** — they have the same permissions as the host process.
Tinkwell does not sandbox plugin code.

For integrity verification, distribute plugins as signed Tinkwell packages.
When you install with `tw plugin install --key publisher.pub`, the package signatures are verified automatically before extraction.
You may pass `--key` multiple times to trust more than one publisher; verification succeeds if the signature matches any of the supplied keys.
This confirms that the package contents have not been tampered with since the publisher signed them.

Package verification is **strict by default**: if you do not supply any `--key` (and the plugin registry is not configured to provide one), installation fails.
The dedicated `--allow-integrity-only` flag accepts verification against the file-hash manifest alone; use it only for local diagnostics.
An attacker who tampers with content can always rewrite the companion `signatures.tw` to match their tampered hashes, so integrity-only mode cannot tell trustworthy packages from malicious ones.

`AssemblyLoadContext` isolation is a feature of the **catalog** only.
Assemblies loaded by explicit path, or assemblies whose name cannot be resolved by the catalog, run in the host's default context with no additional isolation — see "[Non-catalog loads](#non-catalog-loads)" above.
Do not rely on ALC separation as a security boundary; it prevents dependency conflicts, not privileged access.

**Always review plugin source code before installing.** Package signatures verify integrity (the package wasn't modified) and authenticity against a configured trust anchor, not safety (the code is benign).

## Diagnostics

Plugin loading is logged at the `Debug` level.
To see plugin resolution in action:

- Set logging to `Debug` for the `Tinkwell` category.
- Look for messages like:
  - `Plugin catalog: {Count} plugin(s) discovered`
  - `Loading '{Assembly}' from plugin '{Name}@{Version}'`
  - `Plugin '{Plugin}' references {Ref} v{PluginVer} but host has v{HostVer}`

Each `AssemblyLoadContext` is named `plugin:{directoryName}` for identification in diagnostic tools and memory dumps.

## Reference: `TINKWELL_PLUGIN_PATH`

Set this environment variable to override or extend the default plugin search paths.
Paths are separated by `;` and are searched **before** all other sources.

```bash
# Linux/macOS
export TINKWELL_PLUGIN_PATH="/opt/tinkwell/plugins:/home/user/dev/plugins"

# Windows
set TINKWELL_PLUGIN_PATH=C:\plugins;D:\dev\plugins

# In a CI pipeline
TINKWELL_PLUGIN_PATH=./test-plugins dotnet run
```
