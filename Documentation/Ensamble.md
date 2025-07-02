# Ensemble File Syntax Overview

The Ensemble DSL defines how services are composed and hosted within a system. Each file describes a tree of runners, services, and their properties, used to orchestrate process startup, configuration, and inter-process coordination.

## Syntax

```text
[import <import_path>]
...

runner [<name>] <path> [if <condition>] {
    [arguments: <arguments>]
    [ properties {
        [keep_alive: true|false]
        <option>: <value>
        ...
    } ]
    [ service runner [<name> <path>] [if <condition>] {
        [ properties {
            <option>: <value>
            ...
        } ]
    } ]
    ...
}
...
```

Where:

#### `<import_path>`

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
A memorable name for the [runner](./Glossary.md#runner) or [firmlet](./Glossary.md#firmlet). If it's a [simple identifier](./Glossary.md#simple-identifier) (e.g., `my_runner`), it doesn't need to be enclosed in double quotes; otherwise, they are required (e.g., `"my runner"`). If omitted, the [Supervisor](./Glossary.md#supervisor) assigns a UUID. If specified, it must be unique across the [system](./Glossary.md#system) (no two runners/firmlets can have the same name) and cannot start with two underscores.

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

## Syntactic Sugar

To simplify configuration in common scenarios, the `compose` directive can be used to host a library in one of the predefined Tinkwell hosts:

```text
compose <kind> <path> [ {
    <key>: <value>
}]
```

Where:

#### `<kind>`

Indicates the type of host: `service` (to host a service) or `agent` (to host an agent).

### Example

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
