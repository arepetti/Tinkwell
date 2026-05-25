# Tinkwell.Cli.Commands.Init

CLI extension that adds the `tw init` command -- a guided generator that creates configuration files from **wizard packs**.

## What it does

`tw init` walks the user through an interactive questionnaire defined by a wizard pack, then renders one or more output files using Liquid templates (Fluid).
The command is generic: it is not tied to `ensemble.tw` or any specific file format.
Different packs can generate different kinds of files.

## Usage

```bash
# Generate with the default pack (auto-selected when only one exists)
tw init

# Specify a pack by name
tw init --pack tinkwell-ensemble

# Override the primary output path
tw init --output my-ensemble.tw

# Preview without writing files
tw init --dry-run

# Overwrite existing files
tw init --force

# List available packs
tw init --list-packs

# Use packs from a custom directory
tw init --pack-path /path/to/packs
```

## Architecture

```
tw init ──► WizardPackCatalog ──► WizardPackParser ──► WizardPack
                                                          │
                ┌─────────────────────────────────────────┘
                ▼
           WizardSession ──► AnswerBag
                                │
                                ▼
                        TemplateRenderer (Fluid) ──► Generated files
                                                          │
                                                          ▼
                                                 GeneratedFileValidator
```

- **WizardPackCatalog** -- discovers packs under `AppContext.BaseDirectory/packs/init/`, environment variable `TINKWELL_INIT_PACK_PATH`, and explicit `--pack-path`.
- **WizardPackParser** -- parses `.tw` files (`package.tw`, `questions.tw`, `outputs.tw`) into typed records using the standard configuration parser.
  The manifest uses a `package` block with `type = "init-pack"`, matching the standard Tinkwell package format used by plugins.
- **WizardSession** -- walks the question flow interactively with Spectre.Console prompts, evaluating `when` conditions (via NCalc) to control which questions are shown.
- **AnswerBag** -- stores scalar and repeat-group answers, normalizing keys to snake_case.
  Converts to a Fluid `TemplateContext` for rendering.
- **TemplateRenderer** -- renders `.liquid` templates with Fluid against the answer bag's template context.
  Configures shared `TemplateOptions` for `Dictionary<string, object>` member access.
- **GeneratedFileValidator** -- optional validators per output (currently supports `tinkwell-ensemble` which round-trips through `EnsembleParser`).

## Default pack

The `tinkwell-ensemble` pack ships under `packs/init/tinkwell-ensemble/` and generates a complete `ensemble.tw` starter configuration.
It asks about:

- Runner topology (reliable / balanced / compact)
- Core services (store, events, measures)
- Extensions (signals, actions, measure-events, measure-history, wallclock)
- Protocols (CoAP, MQTT, Modbus, TextQuery, I2C, protobuf gateway)
- State machines
- Cross-protocol data routing (measures-to-CoAP, measures-to-MQTT, events-to-MQTT)

## Creating custom packs

See [docs/reference/init-packs.md](../../docs/reference/init-packs.md) for the full authoring guide.
A minimal pack has:

```
my-pack/
  package.tw         # pack metadata (type = "init-pack")
  questions.tw       # guided procedure
  outputs.tw         # declares output files
  template.liquid    # Liquid template
```

## Condition evaluation

The `when` modifier on questions is evaluated by NCalc (via the same `NCalcSync` engine used by `Tinkwell.Expressions`).
All standard NCalc operators are supported (see the [Expressions Reference](../../docs/user-guide/expressions.md)).
Key rules:

- Bare identifiers: `events` (truthy if the answer is `true`)
- Underscore identifiers: `event_persistence`, `text_query` (valid NCalc and Liquid)
- Negation: `!events`, `!measure_history`
- Logical operators: `events && measures`, `coap || mqtt`
- Equality: `modbus_transport == 'tcp'`, `store_storage != 'memory'`
- Parenthesized groups: `mqtt && measures && (modbus || coap)`

Undefined parameters (questions not yet answered) evaluate to `false`.

Question IDs should use **underscores** (`event_persistence`, not `event-persistence`).
Underscores are valid in NCalc identifiers and Liquid variable names, so the same name works everywhere.
The NCalc bracket syntax (`[event-persistence]`) is supported but not recommended.

## Liquid template notes

Templates use native Fluid comparison operators in `{% if %}` tags:

```liquid
{% if topology == "balanced" %}...{% elsif topology == "compact" %}...{% endif %}
{% if resource.binding == "measure" %}...{% endif %}
{% if store_storage != "memory" %}...{% endif %}
```

Boolean answers are tested directly: `{% if events %}...{% endif %}`.
Repeat groups are iterated with `{% for item in group %}...{% endfor %}`.
