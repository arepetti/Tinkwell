# Command Line Interface

The CLI interface lets you  **monitor** and **debug** your Tinkwell application and **integrate** with external application/scripts not written for .NET.

All the sub-commands are accessible with the `tw` command.

| Supervisor                            | Services                              | Measures
|---------------------------------------|---------------------------------------|---------------------------------------|
| [`tw supervisor`](#tw-supervisor)     | [`tw contracts`](#tw-contracts)       | [`tw measures`](#tw-measures)         |
| [`tw runners`](#tw-runners)           |                                       |                                       |

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

##  `tw supervisor`

Interact with the [Supervisor](./Glossary.md#supervisor) using a low level interface.

### SYNOPSIS

```console
tw supervisor send <COMMAND> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
tw supervisor signal <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
tw supervisor restart <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
tw supervisor claim-port <MACHINE> <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
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

```console
tw supervisor claim-port <MACHINE> <NAME> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--confirm|-y]
```
Claim an HTTP(S) port for the runner with the specified name `<NAME>` running on the machine `<MACHINE>`. You do not normally need this command unless you're using `tw` to integrate your own script/application with Tinkwell and you're not using (or you can't use) the provided .NET libraries. You can obtain the runner name (if your process/script has been launched by the Supervisor) in the environment variable `TINKWELL_RUNNER_NAME`.

Like all commands in `tw supervisor` this command needs confirmation then include `-y` if you're calling it from a script/another application.
The returned value is an integer containing the first available port number to use.

### ARGUMENTS

**`<NAME>`**

The command you want to send to the Supervisor. Keep in mind that its commands are structured like a CLI (with arguments, options and quoted strings).

**`<NAME>`**

Name of the runner.

**`<MACHINE>`**

Name of the machine where the runner calling `tw supervisor claim-port` is running. It could be a computer name (like `"my-computer"`) or an IP address.

**`--confirmed`** **`-y`**

All the sub-commands of `tw supervisor` asks for confirmation, use this flag to skip it and proceed without asking.

---

##  `tw runners`

Inspect and manage [runners](./Glossary.md#runners).

### SYNOPSIS

```console
tw runners list [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v] [--columns]
tw runners inspect <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
tw runners get-host <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
tw runners get-name [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--name=<filter>] [--role=<role>] [--host=<address>]
tw runners profile [filter] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```

### DESCRIPTION

Use `tw runners` when you want to debug, monitor and manage your runners.

### COMMANDS

```console
tw runners list [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--filter=<filter>] [--verbose|-v] [--columns]
```
List all the runners currently active in the [system](./Glossary.md#system). Note that if a filter is specified then it's matched against the _name_ of the runners and it does not affect the [firmlets](./Glossary.md#firmlet) (which are always returned in full when `--verbose` is specified).

```console
tw runners inspect <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```
Show detailed information about a specific runner (optionally including its firmlets). It's mostly what you can find in the [ensamble configuration file](./Ensamble.md).

```console
tw runners get-host <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```
Resolve the host name (for example `"https://my-machine:5000"`) of the specified runner. It returns an empty string if the specified runner does not host any gRPC service.

```console
tw runners get-name [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--name=<filter>] [--role=<role>] [--host=<address>]
```
Resolve the full name of a runner starting from a partial match (when using `--name`, see also `[filter]`), its role (with `--role`) or address (with `--host`). `[filter]` is the preferred alternative to `--name` when using `tw runners get-name`. If they're both specified then `[filter]` has the precedence. If more than one runner matches the expression then an error is returned.

```console
tw runners profile [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```
Inspect resources consumed by all the Tinkwell processes.

### ARGUMENTS

**`<name>`**

The name of the runner to inspect (for `tw runners inspect`) or for which you want to resolve the host address (for `tw runners get-host`).

**`[search]`**

Filter to limit the search to runners with a name that matches the specified [expression](#text-matching).

**`--verbose`** **`-v`**

Produce a detailed description of each runner (including - if any - its host and firmlets).

**`--columns`**

Default output (when `--verbose` is not specified) lists all the runners line-by-line. Use this option to organize them in columns.

**`--name`**

Equivalent to `[name]`.

**`--role`**

A system role. This is usually required during the initialization phase when each firmlet has to determine the address of the Discovery service (which can then be used to resolve all the other services).

For example to query for the name of the discovery service:

```console
tw runners get-name --role=discovery
```

If, for example, you obtain the name `"orchestrator"`, you could then use `tw runners list --filter=orchestrator --verbose` to search for its host address (or directly `tw runners get-host orchestrator`). However, to **resolve the Discovery Service address** (and only that one) there is a simpler method: `tw contracts resolve-discovery-address`.

**`--host`**

An address (like `"https://my-machine:5000"`) of a gRPC service of which you want to know the container's name.

---

##  `tw contracts`

Inspects [services](./Glossary.md#service).

### SYNOPSIS

```console
tw contracts list [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>] [--verbose|-v]
tw contracts find <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
tw contracts resolve-discovery-address [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```

### DESCRIPTION

Use `tw contracts` when you want to inspect the services registered in the [system](./Glossary.md#system).

### COMMANDS

```console
tw contracts list [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>] [--verbose|-v]
```
List all the registered services.

```console
tw contracts find <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--verbose|-v]
```
Find the address of the service with the specified name (or family name or alias).

```console
tw contracts resolve-discovery-address [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>]
```
Resolve the address of the Discovery Service. You can use this if you're integrating your own runner and you want to obtain
the Discovery Service (which you can then use to discover all the other services). Calling this method is the prefferred way for external code (not hosted inside one of the default hosts).

### ARGUMENTS

**`[search]`**

Filter to limit the list of [services](./Glossary.md#service). If specified only those services containing the specified text will be returned. The search is case insensitive and matches values in _name_, _family name_ and _aliases_.

**`<name>`**

Name of the service to find. It's an exact match then it's case sensitive. It searches first in _name_ and _aliases_ and then, if there isn't a match, in _family name_. If there is more than one match (because the family name is not unique) then the most appropriate
instance is returned (with the same logic used to return it to the runners).

**`[--host=<host>]`**

Filter the returned list to the serviced exposed at the specified address.

**`--verbose`** **`-v`**

Produce a detailed description of each service.

---

##  `tw measures`

Manage [measures](./Glossary.md#measure).

### SYNOPSIS

```console
tw measures list [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>] [--values] [--verbose|-v]
tw measures read <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
tw measures write <name> <value> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
tw measures create <name> <type> <unit> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
tw measures subscribe <name>... [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
tw measures lint <path> [--exclude=<rule id>] [--strict]
```

### DESCRIPTION

Use `tw measures` when you want to inspect the measures registered in the [system](./Glossary.md#system) or when you want to create, read or write their values.

### COMMANDS

```console
tw measures list [search] [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>] [--values] [--verbose|-v]
```
List all the registered measures, optionally displaying their current value if `--values` is specified. Use `--verbose` to obtain all the fields associated with every measure`.

```console
tw measures read <name> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
```
Read the current value of the measure with the specified name. Note that it does not include the unit of measure (which is the one specified when the measure has been registered). If you do not know the unit then use `tw measures list <measure name>`.

```console
tw measures write <name> <value> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
```
Write a new value for the measure with the specified name. Note that you must include the unit of measure; it does not need to be the same one registered for the measure (the system will perform a conversion) but it must be compatible.

```console
tw measures create <name> <type> <unit> [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
```
Create a new measure with the specified name, type (for example `"Temperature"`) and unit (for example `"DegreesCelsius"`). See the list of [supported units](./Units.md).

```console
tw measures subscribe <name>... [--machine=<machine name>] [--pipe=<pipe name>] [--timeout=<seconds>] [--host=<host>]
```
Subscribe for changes to one or more measures. Each time a measure changes a new line will be printed in the form `name=value`. Note that the unit of measure is not included (use `tw measures list <name>` if you want to query it). The first value(s) printed are the current value(s) and then after each change.

```console
tw measures lint <path> [--exclude=<rule id>] [--strict]
```
Check the specified .twm (Tinkwell Measures configuration) file for errors or bad practices. The return code is non zero if the file contains any known issue. Use `--exclude` if you want to exclude a specific rule.

#### Examples

```bash
tw measures create temp Temperature DegreesCelsius
tw measures write temp "90 °F"
tw measures read temp # It'll display 32.22 °C
```
Create a new measure `"temp"` stored as °C and update it with a value expressed in °F. Subsequent reads show that the value has been converted into the registered unit of measure (because a conversion from °F to °C is possible).

```bash
tw measures subscribe temperature power
```
Subscribe for changes to the two measures `"temperature"` and `"power"`. Terminate the program to stop watching for changes.

### ARGUMENTS

**`[search]`**

When specified it filters the list to include only measures with the specified text in their name. Search is case insensitive. You can use an [expression](#text-matching).

**`<name>`**

Name of a measure.

**`<value>`**

Value of a measure, including its unit of measure. It must be formatted using en-US culture regardless of what your UI settings are. For example half degree Celsius is always expressed as `"0.5 °C"`. If the unit is not the same of the measure you're updating then the service will perform a conversion (if the two measures are compatible!).

**`<type>`**, **`<unit>`**

Type (for example `"Power"` `"Temperature"`) and unit (for example `"Watt"`, `"DegreesCelsius"`). See the list of [supported unit of measure](./Units.md).

**`--verbose`** **`-v`**

Produce a detailed description of each measure.

**`--exclude=<rule id>`** **`-x=<rule id>`**

Exclude the specified rule from the output. It's useful when `tw measures lint` is part of a build process and a non zero return value might stop the build/deployment. If you're absolutely sure that the flagged issue is not a problem then you can instruct the linter to exclude it.

**`--strict`**

Apply stricter rules and the exit code is not zero when all the issues are minor.

---

