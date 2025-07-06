# Ensemble File Syntax Overview

The Ensemble DSL defines how services are composed and hosted within a system. Each file describes a tree of runners, services, and their properties, used to orchestrate process startup, configuration, and inter-process coordination.

## Syntax

```text
[import "<import path>"]
...

runner ["<name>"] "<path>" [if "<condition>"] {
    [arguments: "<arguments>"]
    [properties {
        [keep_alive: true|false]
        <option>: <value>
        ...
    }]
    [service runner ["<name>" "<path>"] [if "<condition">] {
        [properties {
            <option>: <value>
            ...
        }]
    }]
    ...
}
...

compose <kind> "<name>" "<path>" [{
    <key>: <value>
}]
...
```

Where:

#### `<import path>`

The `import` directive must always be at the beginning of the file, before `runner` definitions. Declaration order matters when there are dependencies between [runners](./Glossary.md#runner); imported definitions are bootstrapped before others.

The path is relative to the current file. For example:

```text
// This is the main ensemble.tw file
import "./config/plumbing.tw"
...

// This is ./config/plumbing.tw
import "./watchdog.tw"
...
```

When importing `watchdog.tw` from `plumbing.tw`, the path is relative to the directory where `plumbing.tw` is located.

#### `<name>`
A memorable name for the [runner](./Glossary.md#runner) or [firmlet](./Glossary.md#firmlet). If it's a [simple identifier](./Glossary.md#simple-identifier) (e.g., `my_runner`), it doesn't need to be enclosed in double quotes; otherwise, they are required (e.g., `"my runner"`). If omitted, the [Supervisor](./Glossary.md#supervisor) assigns a UUID. If specified, it must be unique across the [system](./Glossary.md#system) (no two runners/firmlets can have the same name) and it must follows the following rules (even when quoted):
* It cannot contain `[`, `]`, `{`, `}`, `\`, `*`, `:`, `;`, `"` `'`, `=`, `!` and `?`. 
* It cannot start with `+`, `-` or `/` or with two underscores.

When in doubt it's better to stick with a simple identifier.

#### `<path>`
The path (full or relative to the working directory) of the executable or library to load. On Windows, the `.exe` extension is optional.

#### `<condition>`
An [expression](./Expressions.md) that evaluates to a boolean value to determine if the runner/firmlet should be loaded. The expression must be enclosed in double quotes, like `if "platform == 'windows'"`.
The following parameters are available:
* All key/value pairs specified in the `Ensemble:Params` section of `appsettings.json`.
* `os_architecture`: A string describing the OS architecture.
* `cpu_architecture`: A string describing the CPU architecture.
* `platform`: The OS where Tinkwell is running, one of `"windows"`, `"linux"`, `"osx"`, or `"bsd"`.

#### `<arguments>`
Command-line arguments passed to the executable upon startup. For default Tinkwell [hosts](./Glossary.md#host), you can override configuration settings without modifying `appsettings.json`: `arguments: "--Supervisor:CommandServer:ServerName=another_machine"`.

#### `keep_alive`
If `true`, the Supervisor restarts the runner if it exits unexpectedly (with an exit code greater than zero). The default is `true`. Set it to `false` if your process should not be restarted.

#### `<option>` and `<value>`
Additional properties passed to the runner. Their content depends on the specific runner.

#### `service runner`
Used when the outer `runner` is a host for one or more `firmlets` (the inner `service runner` definitions). Note that not every runner is a host. Firmlets loaded within the same host share the same process and can even share DI services. Sharing a process offers performance benefits (especially for gRPC services) but scales poorly and should be used with caution to prevent a buggy firmlet from disrupting others.

### Preprocessor

To simplify configuration and avoid boilerplate, you can use the `compose` directive which replaces a few commonly used parameters with a template:

```text
compose <kind> "<name>" "<path>" [{
    <key>: <value>
}]
```

Where:

#### `<kind>`

Indicates the type of host: `service` (to host a service) or `agent` (to host an agent). You can define your own _kind_ creating a custom template (see [Customization](#customization)).

### Examples

This configuration:

```text
compose service store "Tinkwell.Store.dll"
compose agent reducer "Tinkwell.Reducer.dll" { path: "./config/measures.twm" }
compose agent reactor "Tinkwell.Reactor.dll" { path: "./config/measures.twm" }
```

Is equivalent to:

```text
runner store "Tinkwell.Bootstrapper.GrpcHost" {
    service runner "Tinkwell.Store.dll" {}
    service runner "Tinkwell.HealthCheck.dll" {}
}

runner reducer "Tinkwell.Bootstrapper.DllHost" {
    service runner "Tinkwell.Reducer.dll" {
        properties {
            path: "./config/measures.twm"
        }
    }
}

runner reactor "Tinkwell.Bootstrapper.DllHost" {
    service runner "Tinkwell.Reactor.dll" {
        properties {
            path: "./config/measures.twm"
        }
    }
}
```

#### Customization

You can define your own reusable templates:

* Pick a name, not already in use, for `<kind>`. For example `wasm`.
* Create a file, distributed with the application, named `"compose_wasm.template"`.
* Edit the file with the text replacement you want. You will have these available parameters:
    * `{{ name }}`: replaced with `<name>` (without quotes, even if used).
    * `{{ path }}`: replaced with `<path>` (without quotes).
    * `{{ properties }}`: replaced with all the `<key>` and `<value>` pairs, including `{` and `}`. If not specified then it's `{}`.
    * `{{ host.grpc }}`: replaced with the name of the default runner to host [services](./Glossary.md#service) packaged as libraries.
    * `{{ host.dll }}`: replaced with the name of the default runner to host [agents](./Glossary.md#agent) packaged as libraries.
    * `{{ firmlet.health_check }}`: replaced with the name of the assembly implementing the [Health Check service](./Glossary.md#health-check-service). See [Health monitoring](./Health-monitoring.md) for the overall configuration.
    * `{{ address.supervisor }}`: replaced with the named pipe used to communicate directly with the [Supervisor](./Glossary.md#supervisor) without using the [Orchestrator](./Glossary.md#orchestrator-service).
    
For example, this is the template used for `service`:

```text
runner "{{ name }}" "{{ host.grpc }}" {
  service runner "__{{ name }}___health_check__" "{{ firmlet.health_check }}" {}
  service runner "__{{ name }}___firmlet__" "{{ path }}" {
    properties {{ properties }}
  }
}
```

Keep in mind that the preprocessor performs a simple text substitution!