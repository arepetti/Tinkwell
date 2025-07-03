# Command Line Interface

The CLI interface lets you monitor and debug your Tinkwell application. All the commands are accessible with the `tw` command.

**Commands to interact with the Supervisor**

[`tw supervisor`](#tw-supervisor)
[`tw runners`](#tw-runners)

**Commands to manage services**

[`tw services`](#tw-services)

---
---

## Common Arguments

These arguments are common to most commands.

**`--machine=<machine name>`**

The name of the [machine](./Glossary.md#machine) where the Supervisor is running. The default value is `"."` (the local machine). You might need to change this parameter only if you're trying to send a command to another computer in the network.

**`--pipe=<pipe name>`**

The name of the network pipe on which the Supervisor is listening for commands. The default is `"tinkwell-supervisor-command-server"`. You shouldn't ever need to change this parameter unless you're running two parallel instances of Tinkwell on the same machine (in that case you'd probably started the supervisor with `"--Supervisor:CommandServer:PipeName=Some-Other-Value"`).

**`--timeout=<seconds>`**

Timeout (in seconds) when waiting to estabilish a connection. You should never need to change this value unless you're working with a remote machine on a very slow network (or if you are connecting to the Supervisor in the middle of the initialization sequence).

## Text Matching

Tinkwell CLI supports flexible wildcard-based runner filtering using a syntax similar to shell-style globbing — not full Git pathspec and not regex, but powerful enough for targeted matching. Note that search/filtering is always case insensitive (whilst normal exact matching is always case sensitive).

### Supported Wildcard Syntax

| Pattern       | Meaning                                                   |
|---------------|-----------------------------------------------------------|
| `*`           | Matches any sequence of characters (including none)       |
| `?`           | Matches exactly one character                             |
| `[abc]`       | Matches a single character: `a`, `b`, or `c`              |
| `[^abc]`      | Matches any character **except** `a`, `b`, or `c`         |
| `[a-z]`       | Matches a single character in the given range             |


### Examples

#### Matches
| Filter Pattern       | Matches                                    |
|----------------------|--------------------------------------------|
| `orches*`            | `orchestrator`                             |
| `re?ctor`            | `reactor`                                  |
| `*store`             | `store`, `datastore`, `keystore`           |
| `watch*`             | `watchdog`, `watcher`, `watchman`          |
| `reduc*`             | `reducer`, `reduction-worker`              |
| `*covery`            | `discovery`, `recovery`, `auto-discovery`  |
| `device[0-9]`        | `device1`, `device5`, `device9`            |

#### Negation
Some patterns intentionally won't match certain runners:
| Pattern          | Does **Not** Match                            |
|------------------|-----------------------------------------------|
| `re?ctor`        | Won’t match `reactorX` (extra character)      |
| `orchestrator?`  | Won’t match plain `orchestrator`              |
| `[^r]*`          | Won’t match anything starting with `r`        |

---
---

##  `tw supervisor`

Interact with the [Supervisor](./Glossary.md#supervisor) using a low level interface.

### SYNOPSIS

```console
tw supervisor send <COMMAND> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
tw supervisor signal <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
tw supervisor restart <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
```

### DESCRIPTION

Use `tw supervisor` when you want to send commands directly with the Supervisor and there aren't other higher level alternatives. Note that this low level interface and sending the wrong command might cause the application to crash.

### COMMANDS

```console
tw supervisor send <COMMAND> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]`
```
Send the specified command to the Supervisor and prints the output. In case of errors the Supervisor always use the pattern "Error: _message_", if the command does not have a return value then it always sends "OK".

```console
tw supervisor signal <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]`
```
Send a signal to the Supervisor to notify that the [runner](./Glossary.md#runner) with the specified name has completed its initialization and the bootstrap sequence can continue. A faulty runner might block its own [host](./Glossary.md#host) (which in turn, for `mode=blocking` runners, could block the bootstrapping sequence). You can try to unblock (without waiting for the initialization sequence to timeout) _signaling_ the Supervisor.

```console
tw supervisor restart <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]`
```
Restart the process associated with the runner with the specified name. Note that not all runners are restartable and this might cause the whole application to fail. Use it only as a last resort.

### ARGUMENTS

**`--confirmed`** **`-y`**

All the sub-commands of `tw supervisor` asks for confirmation, use this flag to skip it and proceed without asking.

---
---

##  `tw runners`

Inspect and manage [runners](./Glossary.md#runners).

### SYNOPSIS

```console
tw runners list [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--filter=<filter>] [--verbose|-v] [--columns]
tw runners inspect <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
tw runners get-host <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
tw runners get-name [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--name=<filter>] [--role=<role>] [--host=<address>]
```

### DESCRIPTION

Use `tw runners` when you want to debug, monitor and manage your runners.

### COMMANDS

```console
tw runners list [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--filter=<filter>] [--verbose|-v] [--columns]
```
List all the runners currently active in the [system](./Glossary.md#system).

```console
tw runners inspect <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```
Show detailed information about a specific runner (optionally including its firmlets). It's mostly what you can find in the [ensamble configuration file](./Ensamble.md).

```console
tw runners get-host <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```
Resolve the host name (for example `"https://my-machine:5000"`) of the specified runner. It returns an empty string if the specified runner does not host any gRPC service.

```console
tw runners get-name [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--name=<filter>] [--role=<role>] [--host=<address>]
```
Resolve the full name of a runner starting from a partial match (when using `--name`, see also `[name]`), its role (with `--role`) or address (with `--host`).

### ARGUMENTS

**`<name>`**

The name of the runner to inspect (for `tw runners inspect`) or for which you want to resolve the host address (for `tw runners get-host`).

**`[name]`**

Filter to resolve the name for the runner with a name that matches the specified [expression](#text-matching). This is an alternative to `--name` when using `tw runners get-name`. If they're both specified then this has the precedence. Use this search when you don't know the exact name of a runner but you know _more or less_ how it looks like. If more than one runner matches the expression then an error is returned.

**`--filter=<filter>`**

Filter to include only the top level runners that match the specified [expression](#text-matching). Note that this filter only applies to the _name_ of the runners and it does not affect the [firmlets](./Glossary.md#firmlet) (which are always returned in full when `--verbose` is specified).

**`--verbose`** **`--v`**

Produce a detailed description of each runner (including - if any - its host and firmlets).

**`--columns`**

Default output (when `--verbose` is not specified) lists all the runners line-by-line. Use this option to organize them in columns.

**`--name`** **`-n`**

Equivalent to `[name]`.

**`--role`** **`-r`**

A system role. This is usually required during the initialization phase when each firmlet has to determine the address of the Discovery service (which can then be used to resolve all the other services). Use this option when you need, for example, to determine which host has taken the role of Discovery Service.

For example to query for the name of the discovery service:

```console
tw runners get-name --role=discovery
```

If, for example, you obtain the name `"orchestrator"`, you could then use `tw runners list --filter=orchestrator --verbose` to search for its host address to create an instance of the Discovery Service client. Or, if running a script, directly `tw runners get-host orchestrator`.

**`--host`** **`-h`**

An address (like `"https://my-machine:5000"`) of a gRPC service of which you want to know the container's name.

---
---

##  `tw services`

Inspects [services](./Glossary.md#service).

### SYNOPSIS

```console
tw services list [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
tw services find <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```

### DESCRIPTION

Use `tw services` when you want to inspect the services registered in the [system](./Glossary.md#system).

### COMMANDS

```console
tw services list [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```
List all the registered services.

```console
tw services find <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```
Find the address of the service with the specified name (or family name or alias).

### ARGUMENTS

**`[filter]`**

Filter to limit the list of [services](./Glossary.md#service). If specified only those services containing the specified text will be returned. The search is case insensitive and matches values in _name_, _family name_ and _aliases_.

**`<name>`**

Name of the service to find. It's an exact match then it's case sensitive. It searches first in _name_ and _aliases_ and then, if there isn't a match, in _family name_. If there is more than one match (because the family name is not unique) then the most appropriate
instance is returned (with the same logic used to return it to the runners).

**`--verbose`** **`--v`**

Produce a detailed description of each service.

---
---

