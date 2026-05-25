# Project README Template

Every project under `src/` should have a `README.md`.
The expected content depends on whether the library is published to NuGet.

## Published libraries (`src/app/libs/`)

Published library READMEs are packed into the NuGet package and displayed on nuget.org.
They must be **self-contained** — they cannot link to files in `docs/` since those aren't available to NuGet consumers.

Expected content:

1. **Title and one-line description** — what the library does.
2. **"Part of Tinkwell" banner** — link to the GitHub repository.
3. **Quick start** — install and basic usage with code examples.
4. **API overview** — key types, methods, and patterns.
5. **Configuration/options** — tables for settings, manifest properties, etc.
6. **Link to the CI tool** — if relevant (e.g. `Tinkwell.Package` links to `Tinkwell.Build.Ci`).

Do NOT link to `docs/` files.
Do NOT duplicate content that only matters to Tinkwell contributors.

## Non-published projects (`src/` outside `libs/`)

These READMEs are for contributors navigating the codebase.
Keep them concise.

Expected content:

1. **One-line purpose** — what this project does in the system.
2. **Role in the architecture** — how it fits into the coordinator/runner/runlet model.
   Link to the relevant architecture doc rather than repeating it.
3. **Key types** — the 2-3 most important public or internal types and what they do.
   Not an exhaustive API list.
4. **Configuration** (if applicable) — which `.tw` blocks this project parses and links to the relevant reference page.
5. **Ordering or dependency constraints** — if this runlet must be declared in a specific order or depends on specific services.

Do NOT:
- Repeat the project description from the `.csproj`.
- Explain what a runner or runlet is (link to architecture docs).
- List every class in the project.
- Include user-facing configuration examples (link to `docs/reference/` or `docs/user-guide/configuration.md`).

## Example (non-published)

```markdown
# Tinkwell.Runlet.Mqtt

MQTT integration runlet. Connects to MQTT brokers, subscribes to topics, and
routes incoming messages through binding chains.

## Architecture

Headless runlet (`IRunlet`). The `MqttConnectionWorker` manages broker
connections and a bounded message channel. Incoming messages are dispatched
through `IMqttBinding` implementations discovered from `.tw` configuration.

Uses `IServiceDiscovery` to find the event bus and other services required
by bindings. See [MQTT reference](docs/reference/mqtt.md) for configuration
syntax and examples.

## Key types

- `MqttRunlet` — the `IRunlet` entry point; registers config and workers.
- `MqttConnectionWorker` — `BackgroundService` managing one broker connection.
- `MqttConfigWorker` — parses `mqtt` blocks and creates connection workers.

## Dependencies

- **Events service** — for the `event` binding.
- **Measures service** — for the `measure` binding (optional).
- **Store service** — for the `store` binding (optional).
```
