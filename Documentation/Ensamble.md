# Ensamble Configuration

This document describes the Ensamble DSL, which defines how services and agents are composed and hosted within a Tinkwell system. Each file (typically with a `.tw` extension) describes a tree of runners, services, and their properties, used by the **Supervisor** to orchestrate process startup and configuration.

You can create a starting poin to customize using:

```bash
tw templates use ensamble_configuration
```

## Syntax Overview

The most common way to define the system composition is by using the `compose` directive. You can also include other files with `import`.

```text
// Import other definitions
import "shared_services.tw"

// Compose a standard service
compose service store "Tinkwell.Store.dll"

// Compose an agent with properties
compose agent reducer "Tinkwell.Reducer.dll" {
    path: "./config/measures.twm"
}
```

## Preprocessor Directives

The preprocessor evaluates certain directives before the main parser runs, allowing for more flexible and maintainable configurations.

### The `compose` directive

The `compose` directive is a high-level abstraction for running standard Tinkwell services and agents without needing to write a full `runner` definition. It uses predefined templates to generate the underlying configuration.

`compose <kind> <name> "<path>" [if "<condition>"] [{ <properties> }]`

-   `<kind>`: The type of component to compose. Built-in kinds are `service` and `agent`.
-   `<name>`: A unique name for the component.
-   `<path>`: The path to the component's DLL.
-   `if "<condition>"`: An optional expression for conditional loading (see `runner` attributes below).
-   `<properties>`: An optional block of key-value pairs to configure the component.

For example, this high-level configuration:

```text
compose service store "Tinkwell.Store.dll"
compose agent reducer "Tinkwell.Reducer.dll" { path: "./config/measures.twm" }
```

...is expanded by the preprocessor into this full `runner` configuration:

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
```

## Imports

The `import` directive allows you to include definitions from other files, which is useful for organizing complex systems. It must appear at the top of the file. The path is relative to the directory of the current file.

`import "./path/to/another_file.tw"`

## Common Hosts and Firmlets

While the `runner` block is generic, a typical Tinkwell system is built by composing a set of standard hosts and firmlets.

### Hosts

Hosts are special runners designed to load and manage one or more firmlets, providing a shared environment.

-   **`Tinkwell.Bootstrapper.DllHost`**: A general-purpose host for loading firmlets (typically agents) packaged as .NET DLLs. It does not expose any network services on its own.
-   **`Tinkwell.Bootstrapper.GrpcHost`**: A host designed specifically for hosting firmlets that expose gRPC services. It manages the gRPC server lifecycle.

### Firmlets

Firmlets are the core components that provide the application's logic. They are loaded by hosts.

-   **`Tinkwell.Orchestrator`**: A service that exposes commands to manage runners and the supervisor itself.
-   **`Tinkwell.Store`**: A service that acts as the central database for all measures, handling storage, unit conversion, and broadcasting changes. The store supports two configuration properties to change the default implementation:
    * `storage_strategy`: it's the name of an alternative storage where the measure data is kept. Default implementation keeps them in memory and it resets each time. You can use `"SqliteStorage"` to permanently store the measures in a local SQLite DB. You may also specify a `sqlite_connection_string` property (default to a file `measures.db` file in the application data folder).
    * `registry_storage`: it's the name of an alternative management layer that validate input data and coordinate data transfer from and to the storage. There are no alternative implementations.   
-   **`Tinkwell.EventsGateway`**: A service that provides a publish/subscribe message bus for system-wide events.
-   **`Tinkwell.Executor`**: An agent that executes actions based on event triggers. It requires a `path` property pointing to an actions configuration file (`.twa`).
    ```text
    compose agent executor "Tinkwell.Actions.Executor.dll" {
        path: "./config/actions.twa"
    }
    ```
-   **`Tinkwell.Reducer`**: An agent that calculates derived measures based on a configuration file. It requires a `path` property pointing to a measures configuration file (`.twm`).
    ```text
    compose agent reducer "Tinkwell.Reducer.dll" {
        path: "./config/measures.twm"
    }
    ```
-   **`Tinkwell.Reactor`**: An agent that monitors the Store for changes and emits signals when conditions are met. It also requires a `path` to a measures configuration file (`.twm`) where signals are defined.
    ```text
    compose agent reactor "Tinkwell.Reactor.dll" {
        path: "./config/measures.twm"
    }
    ```
-   **`Tinkwell.HealthCheck`**: A service that provides basic health monitoring for the host it's running in. It's automatically included by the `compose service` directive.
-   **`Tinkwell.Watchdog`**: A firmlet that periodically queries all health check services and reports on their status.
-   **`Tinkwell.Bridge.MqttClient`**: An agent that receives MQTT messages from a broker and write sensor data into the store. See [MQTT Bridge](./MQTT-Bridge.md) for all the configuration options.
-   **`Tinkwell.Bridge.MqttServer`**: An agent that implements a super simple MQTT broker for local development and testing. See [MQTT Server](./MQTT-Server.md) for all the configuration options.

## Advanced: The `runner` block

The `runner` block is the underlying, low-level syntax for defining a process to be managed by the Supervisor. The `compose` directive is a simplified frontend for this block.

`runner ["<name>"] "<path>" [if "<condition>"] { ... }`

> **A Note on Naming**
>
> While names can be quoted strings to include spaces or dots, they must still adhere to certain rules.
>
> -   **Invalid Characters:** Names cannot contain `[`, `]`, `{`, `}`, `\`, `*`, `:`, `;`, `"`, `'`, `=`, `!`, or `?`.
> -   **Invalid Prefixes:** Names cannot start with `+`, `-`, `/`, or `__` (two underscores).
>
> For simplicity, it is recommended to use [simple identifiers](./Glossary.md#simple-identifier).


### Attributes

| Attribute | Type | Description |
| :--- | :--- | :--- |
| `name` | String | A unique name for the runner. Optional, but recommended. |
| `path` | String | The path (full or relative) to the executable or library to load. |
| `if "<condition>"` | String | **Conditional Loading:** An [expression](./Expressions.md) evaluated when the file is loaded. If `false`, the runner is ignored. Available parameters include `platform`, `os_architecture`, `cpu_architecture`, and any custom key/value pairs from `appsettings.json`. |
| `arguments` | String | Command-line arguments to pass to the executable upon startup. |
| `properties` | Dictionary | A block of key-value pairs specific to the runner. A common property is `keep_alive: "true"` (the default), which tells the Supervisor to restart the runner if it exits unexpectedly. Note that all values MUST be strings. |

### Nested Firmlets

A `runner` can act as a host for one or more `firmlets` (defined with `service runner`). Firmlets loaded within the same host share the same process, which can improve performance but reduces isolation.

```text
// A gRPC host runner containing multiple firmlets
runner measures "Tinkwell.Bootstrapper.GrpcHost" {
    service runner "Tinkwell.Store.dll" {}
    service runner "Tinkwell.Reducer.dll" {}
    service runner "Tinkwell.Reactor.dll" {}
    service runner "Tinkwell.HealthCheck.dll" {}
}
```

## Customization

You can define your own reusable templates for the `compose` directive.

-   Pick a name for your new kind, for example, `wasm`.
-   Create a file named `compose_wasm.template` and distribute it with your application.
-   Edit the file with the `runner` definition you want to generate. You can use the following parameters for substitution:
    -   `{{ name }}`: The `<name>` provided to the `compose` directive.
    -   `{{ path }}`: The `<path>` provided to the `compose` directive.
    -   `{{ properties }}`: The entire properties block (`{...}`) provided to the `compose` directive. Defaults to `{}` if omitted.
    -   `{{ host.grpc }}`: The name of the default runner for hosting gRPC services.
    -   `{{ host.dll }}`: The name of the default runner for hosting agents and other DLL-based components.
    -   `{{ firmlet.health_check }}`: The name of the Health Check service assembly.
    -   `{{ address.supervisor }}`: The named pipe for direct communication with the Supervisor.

For example, this is the built-in template for the `service` kind:

```text
runner "{{ name }}" "{{ host.grpc }}" {
  service runner "__{{ name }}___health_check__" "{{ firmlet.health_check }}" {}
  service runner "__{{ name }}___firmlet__" "{{ path }}" {
    properties {{ properties }}
  }
}
```

> **Note:** The preprocessor performs a simple text substitution before the file is parsed.

## Complete Example

```text
// File: /etc/tinkwell/ensamble.tw

// Import shared definitions, which might contain the 'store' service
import "shared_services.tw"

// Conditionally compose the watchdog service only on linux
compose service watchdog "Tinkwell.Watchdog.dll" if "platform == 'linux'"

// Compose agents
compose agent reducer "Tinkwell.Reducer.dll" {
    path: "./config/measures.twm"
}

compose agent reactor "Tinkwell.Reactor.dll" {
    path: "./config/measures.twm"
}

// Use the advanced runner syntax for a custom, non-Tinkwell process
runner "data_importer" "/usr/bin/data-importer" {
    arguments: "--source /var/data/source --interval 300"
    properties {
        keep_alive: "true"
    }
}
```

## Default Configuration

The following is the standard `ensamble.tw` configuration file that is included with a default Tinkwell installation. It composes all the core services and agents needed for a fully functional system.

```text
compose service orchestrator "Tinkwell.Orchestrator.dll"
compose service store "Tinkwell.Store.dll"
compose service events "Tinkwell.EventsGateway.dll"
compose agent executor "Tinkwell.Actions.Executor.dll" { path: "./config/actions.twa" }
compose agent reducer "Tinkwell.Reducer.dll" { path: "./config/measures.twm" }
compose agent reactor "Tinkwell.Reactor.dll" { path: "./config/measures.twm" }
compose agent watchdog "Tinkwell.Watchdog.dll"
```

If you want to customize the configuration you should use the `tw templates create` command and select the appropriate starting point, the tool will ask you questions to create the perfect starting point for your specific needs.