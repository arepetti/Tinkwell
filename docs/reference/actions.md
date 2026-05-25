# Actions

The **actions** runlet subscribes to the Tinkwell event bus and runs configurable **handlers** when incoming events match filters you define in `.tw` files.
It is the primary way to automate responses to signals, measure changes, and other events (log lines, HTTP calls, follow-up events, store updates, and more).

**Prerequisite:** The [event bus](events.md) (`Tinkwell.Runlet.Events`) must be running in a runner that starts before producers.
Actions and other consumers typically live in the same or a later runner.

## Event model

Actions react to `EventEnvelope`-shaped events (Subject–Verb–Object).
Filters and expressions use the same field names as in [Events](events.md) (`Source`, `Verb`, `Name`, `Object`, `CorrelationId`, `Timestamp`, plus payload keys).

## Configuration overview

Actions are declared with top-level `action` blocks in a `.tw` file.
The actions runlet loads that file at startup (see **Runlet settings** below).

### Syntax

```tw
action <name> [when <event-name>] {
    [source = <filter>]
    [verb = <filter>]
    [on error <policy> [retry N] [delay N] [backoff N];]

    do <handler-name> [from "<assembly>"] {
        <param> = <value>
        ...
        [on error <policy> [retry N] [delay N] [backoff N];]
    }
}
```

| Modifier | Description |
|----------|-------------|
| `when <name>` | Optional. Restricts matching to events whose `Name` equals this value (case-insensitive). |

### Filters

Body properties narrow which events invoke the action:

| Property | Description |
|----------|-------------|
| `source` | Matches `EventEnvelope.Source` (e.g. `signals`, `measures`). |
| `verb` | Matches the event verb (e.g. `fired`, `changed`). |

### Handler blocks (`do`)

Each `do` block runs one handler.
You can list several `do` blocks per action.
The runlet provides `log`, `create-event`, `http-post`, and `text-send` without `from`.
Other handlers usually come from **`Tinkwell.Actions`** when `from` is omitted (default assembly).
Specify `from "<assembly>"` for custom handler DLLs.

### Error policies

`on error` may appear on the action (default for all handlers) or on a specific `do` (overrides the action default).

| Policy | Behavior |
|--------|----------|
| `on error resume next;` | Log a warning, skip the failed handler, continue. **Implicit default.** |
| `on error stop this;` | Log an error and disable this handler for future invocations. |
| `on error stop application;` | Log critical and shut down the application. |
| `on error publish "event-name" { ... }` | Publish a failure event, then continue. |

Optional retry modifiers: `retry N` runs the handler up to `1 + N` times total; `delay N` (ms, default 1000) and `backoff N` (multiplier, default 1) control pauses between retries (delay × backoff^attempt).

## Expression model

When an action runs, the triggering event is exposed to parameter expressions:

| Variable | Type | Description |
|----------|------|-------------|
| `Source` | string | Event source. |
| `Verb` | string | Lowercase verb name. |
| `Name` | string | Event name. |
| `Object` | string? | Object or value. |
| `CorrelationId` | string? | Correlation id. |
| `Timestamp` | DateTime | UTC timestamp. |

Payload entries are merged into the expression scope; event properties win on name clashes.

Use **`(format("... {Name} ..."))`** for runtime string interpolation with named placeholders.
Unquoted identifiers and `"..."` strings are static; `$"..."` is resolved at parse time.

## Runlet settings

Configure the actions runlet on the runner (kebab-case keys):

| Setting | Type | Description |
|---------|------|-------------|
| `path` | `string?` | Path to the `.tw` file that contains `action` blocks. Defaults to the coordinator’s configuration file when omitted. |

## Ensemble configuration

Place `actions` in a runner that can reach the events service (often the same runner as `text-query`, `signals`, etc.):

```tw
runner background from "Tinkwell.Runner.Headless.dll" {
    runlet text-query from "Tinkwell.Runlet.TextQuery.dll";
    runlet actions    from "Tinkwell.Runlet.Actions.dll" {
        path = "ensemble.tw"
    }
}
```

The events bus itself is usually in a dedicated gRPC runner; see [Events — Ensemble configuration](events.md#ensemble-configuration).

## Handlers

The runlet registers built-in handlers `log`, `create-event`, `http-post`, and `text-send`.
Additional handlers are provided by **`Tinkwell.Actions`** (measures and store).
Parameter tables, built-in vs. external assemblies, and **authoring custom handlers** (`IActionHandler`) are documented in the [Actions runlet README](https://github.com/arepetti/Tinkwell/blob/main/src/app/Tinkwell.Runlet.Actions/README.md).

## See also

- [Events](events.md) — bus model, subscribe filters, delivery semantics
- [Signals](signals.md) — conditions that produce events actions can consume
- [Measures](measures.md) — measure types and updates
