# Wizard Packs (`tw init`)

The `tw init` command is a guided generator that creates configuration files from **wizard packs**.
A pack defines a set of questions and Liquid templates that produce output files.
The command is generic -- it is not tied to any specific file format and can generate anything from `ensemble.tw` configurations to state machine skeletons.

## Quick start

```bash
tw init                       # run with the default pack
tw init --pack tinkwell-ensemble  # pick a specific pack
tw init --output my.tw            # override the primary output path
tw init --dry-run                 # preview without writing
tw init --force                   # overwrite existing files
tw init --list-packs              # show all available packs
```

## Command options

| Option           | Description                                      |
|------------------|--------------------------------------------------|
| `--pack`, `-p`   | Pack name or directory path                      |
| `--output`, `-o` | Override the primary output file path             |
| `--force`        | Overwrite existing files without prompting        |
| `--dry-run`      | Preview generated files without writing them      |
| `--list-packs`   | List available packs and exit                     |
| `--pack-path`    | Additional directory to search for packs          |

## Pack discovery

Packs are discovered from three roots, in order:

1. **App-local**: `{app-directory}/packs/init/{pack-name}/`
2. **Environment**: directory in `TINKWELL_INIT_PACK_PATH`
3. **Explicit**: directory passed via `--pack-path`

Each pack directory must contain a `package.tw` manifest file with `type = "init-pack"`.

## Pack structure

A pack directory contains `.tw` files for metadata and questions, plus `.liquid` files for output templates:

```
my-pack/
  package.tw         # pack metadata (type = "init-pack")
  questions.tw       # the guided procedure (questions, repeats)
  outputs.tw         # declares which files to generate
  template.liquid    # one or more Liquid output templates
```

### `package.tw`

The manifest uses the standard Tinkwell `package` block (the same format used by [plugins](plugins.md) and [packages](packages.md)), with `type = "init-pack"` to identify it as a wizard pack.
Pack-specific properties (`primary-output`, `questions`, `outputs`) are stored alongside the standard metadata fields.

```tw
package my-pack {
    type = "init-pack"
    description = "Generate a configuration file"
    primary-output = "config.tw"
    questions = "questions.tw"
    outputs = "outputs.tw"
}
```

| Property         | Required | Description                                      |
|------------------|----------|--------------------------------------------------|
| `type`           | yes      | Must be `"init-pack"`                            |
| `title`          | no       | Display name shown during the wizard             |
| `description`    | no       | Short description (standard `package` field)     |
| `primary-output` | yes      | Default output file name                         |
| `questions`      | yes      | Relative path to the questions file              |
| `outputs`        | yes      | Relative path to the outputs file                |
| `version`        | no       | Pack version (standard `package` field)          |
| `author`         | no       | Primary author (standard `package` field)        |

All standard `package` metadata fields (`version`, `author`, `license`, etc.) are accepted and passed through.
See the [packages reference](packages.md) for the full list.

### `questions.tw`

Contains the guided procedure.
The file has a single top-level `questions` block with nested `question` and `repeat` blocks.

```tw
questions my-pack {

    question name {
        type = text
        prompt = "What is the name?"
        default = "example"
    }

    question enable_feature {
        type = confirm
        prompt = "Enable the feature?"
        default = true
    }

    question mode when (enable_feature) {
        type = choice
        prompt = "Which mode?"
        default = fast

        option fast {
            label = "Fast mode"
        }

        option safe {
            label = "Safe mode"
        }
    }
}
```

#### Question types

| Type      | Prompt style                | Answer type |
|-----------|-----------------------------|-------------|
| `confirm` | Yes/No                      | boolean     |
| `text`    | Free text input             | string      |
| `integer` | Numeric input               | integer     |
| `choice`  | Selection from `option` list | string (option ID) |

#### Question properties

| Property      | Required | Description                                                    |
|---------------|----------|----------------------------------------------------------------|
| `type`        | yes      | One of `confirm`, `text`, `integer`, `choice`                  |
| `prompt`      | yes      | Text shown to the user                                         |
| `description` | no       | Longer explanation shown below the prompt; disappears after the user answers |
| `default`     | no       | Default value                                                  |

#### Options (for `choice` questions)

```tw
option fast {
    label = "Fast mode"
}
```

The option's block name becomes the answer value.
The `label` property is shown to the user during selection.

#### Conditional questions (`when`)

Questions can have a `when` modifier that controls whether they are shown.
The condition is evaluated at wizard runtime against the current answer bag.

```tw
question detail when (enable_feature) {
    type = text
    prompt = "Feature detail"
}
```

**Important**: `when` is a *wizard-runtime* modifier.
It is different from the `.tw` preprocessor's `if` modifier, which is evaluated at parse time.
The wizard evaluates `when` conditions dynamically as answers are collected.

#### Condition syntax

The `when` expression follows NCalc conventions (see [Expressions Reference](../user-guide/expressions.md)).

| Syntax                          | Meaning                                        |
|---------------------------------|------------------------------------------------|
| `events`                        | True if `events` answer is truthy              |
| `event_persistence`             | True if `event_persistence` answer is truthy   |
| `!events`                       | True if `events` answer is falsy               |
| `a && b`                        | True if both `a` and `b` are truthy            |
| `a \|\| b`                      | True if either `a` or `b` is truthy            |
| `x == 'value'`                 | True if `x` equals the string `value`          |
| `x != 'value'`                 | True if `x` does not equal `value`             |
| `(a \|\| b) && c`              | Parenthesized grouping                         |

Truthy values: `true`, non-empty strings (except `"false"`), non-zero numbers.
Falsy values: `false`, empty string, `"false"`, `0`, unanswered questions.

**Naming convention**: use **underscores** (`event_persistence`, not `event-persistence`) for question IDs.
Underscores are valid in NCalc identifiers and in Liquid variable names, so the same name works everywhere without conversion or bracket escaping.
The NCalc bracket syntax (`[event-persistence]`) is supported and `-` is converted to `_` in Liquid.

### Repeat groups

A `repeat` block asks a count question, then repeats its child questions for each item.
Items are collected into an array that can be iterated in Liquid templates with `{% for %}`.

```tw
repeat resources when (coap) {
    item-name = resource
    item-label = "CoAP resource"

    count resources {
        prompt = "How many resources?"
        default = 1
        minimum = 1
    }

    question path {
        type = text
        prompt = "Resource {index} path"
        default = "/sensor/+"
    }

    question binding {
        type = choice
        prompt = "Binding for resource {index}"
        default = measure

        option measure {
            label = "Read/write a measure"
        }

        option event {
            label = "Publish an event"
        }
    }
}
```

| Property     | Required | Description                              |
|--------------|----------|------------------------------------------|
| `item-name`  | yes      | Identifier for each item in the group    |
| `item-label` | no       | Display label for each item              |

The `count` block has:

| Property  | Required | Description              |
|-----------|----------|--------------------------|
| `prompt`  | yes      | Text shown to the user   |
| `default` | no       | Default count (default: 1) |
| `minimum` | no       | Minimum count (default: 1) |
| `maximum` | no       | Maximum count            |

In prompt and default strings, `{index}` is replaced with the 1-based item number at runtime.

### `outputs.tw`

Declares which files to generate and which Liquid template to use.
Each `output` block maps to one generated file.

```tw
outputs my-pack {
    output config {
        path = "config.tw"
        render = "config.liquid"
        validator = tinkwell-ensemble
    }

    output readme when (create_readme) {
        path = "README.md"
        render = "readme.liquid"
    }
}
```

| Property    | Required | Description                              |
|-------------|----------|------------------------------------------|
| `path`      | yes      | Output file path                         |
| `render`    | yes      | Relative path to the Liquid template     |
| `validator` | no       | Validator name for post-generation check |

Outputs can also have `when` conditions that are evaluated against the answer bag.

#### Validators

The `validator` property names a built-in validator that checks the generated output.
Currently supported:

| Name                  | Description                                    |
|-----------------------|------------------------------------------------|
| `tinkwell-ensemble`   | Parses the output through `EnsembleParser` in lax mode |

## Liquid templates

Output templates are standard Liquid files rendered by Fluid.
They have access to all answers as template variables.

### Variable naming

Answer IDs use **snake_case** in `.tw` files (e.g. `mqtt_broker`).
Because underscores are valid in both NCalc identifiers and Liquid variable names, the same name works in `when` conditions, `{{ }}` expressions, and `{% if %}` tags without any conversion.

### Available variables

- **Scalar answers**: `{{ store_storage }}`, `{{ mqtt_port }}`
- **Boolean answers**: `{% if events %}...{% endif %}`
- **Repeat groups**: `{% for resource in coap_resources %}...{% endfor %}`
- **Repeat item fields**: `{{ resource.path }}`, `{{ resource.binding }}`

### String comparisons

Liquid templates support native `==` and `!=` operators in `{% if %}` and `{% elsif %}` tags.
Use them to test choice-type answers directly:

```liquid
{% if topology == "balanced" %}
...balanced layout...
{% elsif topology == "compact" %}
...compact layout...
{% endif %}
```

This also works inside repeat-group items:

```liquid
{% for resource in coap_resources %}
{% if resource.binding == "measure" %}
...
{% endif %}
{% endfor %}
```

### Template example

```liquid
# Generated configuration
{% if topology == "balanced" %}
runner grpc-services from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "{{ store_storage }}"
    }
{% if events %}
    runlet events from "Tinkwell.Runlet.Events.dll";
{% endif %}
}
{% elsif topology == "compact" %}
runner main from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "{{ store_storage }}"
    }
}
{% endif %}

{% if coap %}
coap sensors {
    port = {{ coap_port }}
{% for resource in coap_resources %}
    resource "{{ resource.path }}" {
{% if resource.binding == "measure" %}
        on get {
            bind measure {
                name = (segment(path, -1))
            }
        }
{% endif %}
    }
{% endfor %}
}
{% endif %}
```

## Built-in pack: `tinkwell-ensemble`

The default pack generates a complete `ensemble.tw` starter configuration.
It covers:

### Topology

Determines how runners are organized:

- **Reliable**: each core service in its own runner (best fault isolation)
- **Balanced**: gRPC services grouped, headless tasks separate (default)
- **Compact**: everything in as few runners as possible

### Core services

- **State store**: in-memory or SQLite backend
- **Event bus**: optional, with optional SQLite persistence
- **Measures**: optional, with optional signals and measure-events bridge

### Extensions

- **Actions**: event-driven handlers (requires events)
- **Measure history**: time-series persistence with configurable backend
- **Wallclock**: periodic timestamps (auto-included with state machines)
- **State machines**: declarative state machines (requires measures)

### Protocols

- **CoAP**: UDP server with configurable resources and bindings.
  Supports repeatable resource definitions with measure, event, or store bindings.
- **MQTT**: subscriber with configurable broker, topic, and binding type.
  Optional embedded broker for local development.
- **Modbus**: TCP or RTU polling
- **TextQuery**: generic text-based data acquisition (TCP, serial, file, command)
- **I2C**: Linux-only I2C bus polling
- **Protobuf gateway**: CoAP-to-gRPC tunneling

### Data routing

When multiple protocols are enabled, the wizard offers cross-protocol routing:

- **Measures to CoAP**: adds a generic `GET /measures/+` resource so CoAP clients can read measures written by other protocols
- **Measures to MQTT**: forwards measure changes to MQTT (auto-enables measure-events bridge and actions)
- **Events to MQTT**: forwards events to MQTT

## Creating a new pack

1. Create a directory under `packs/init/` (or any discoverable root)
2. Add `package.tw` with `type = "init-pack"` and the pack metadata
3. Add `questions.tw` with your question flow
4. Add `outputs.tw` declaring each output file and its template
5. Add `.liquid` templates for each output

The question flow can include any combination of `confirm`, `text`, `integer`, and `choice` questions, with `when` conditions and `repeat` groups for dynamic content.

### Tips

- Use **underscores** in question IDs (`mqtt_broker`, not `mqtt-broker`) -- they work as NCalc identifiers and Liquid variables without escaping
- Keep questions ordered logically -- users see them in definition order
- Use `when` conditions to avoid asking irrelevant questions
- Use repeat groups for user-defined collections (resources, devices, etc.)
- Use `{index}` in prompts and defaults for numbered items
- In Liquid templates, use `{% if var == "value" %}` for choice comparisons and `{% if flag %}` for booleans
- Use `elsif` to collapse mutually-exclusive branches instead of separate `if` blocks
- Add a `validator` to outputs that should be parseable (e.g. `.tw` files)
