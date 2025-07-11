# Command Line Interface

The CLI interface lets you  **monitor** and **debug** your Tinkwell application and **integrate** with external application/scripts not written for .NET.

All the sub-commands are accessible with the `tw` command.

**Initial setup**: [`tw certs`](#tw-certs), [`tw ensamble`](#tw-ensamble), [`tw measures`](#tw-measures)

**Measures and alarms**: [`tw measures`](#tw-measures), [`tw events`](#tw-events), [`tw executor`](#tw-executor)

**Events**: [`tw events`](#tw-events), [`tw executor`](#tw-executor)

**Integrations**: [`tw supervisor`](#tw-supervisor), [`tw contracts`](#tw-contracts)

**Debug**: [`tw supervisor`](#tw-supervisor), [`tw runners`](#tw-runners), [`tw contracts`](#tw-contracts)

---

## Common Arguments

These arguments are common to most commands. In the synopsis they're omitted and if available you'll see `[...shared...]`.

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
tw supervisor send <COMMAND> [...shared...] [--confirm|-y]
tw supervisor signal <NAME> [...shared...] [--confirm|-y]
tw supervisor restart <NAME> [...shared...] [--confirm|-y]
tw supervisor claim-port <MACHINE> <NAME> [...shared...] [--confirm|-y]
```

### DESCRIPTION

Use `tw supervisor` when you want to send commands directly with the Supervisor and there aren't other higher level alternatives. Note that this low level interface and sending the wrong command might cause the application to crash.

### COMMANDS

```console
tw supervisor send <COMMAND> [...shared...] [--confirm|-y]`
```
Send the specified command to the Supervisor and prints the output. In case of errors the Supervisor always use the pattern "Error: _message_", if the command does not have a return value then it always sends "OK".

```console
tw supervisor signal <NAME> [...shared...] [--confirm|-y]`
```
Send a signal to the Supervisor to notify that the [runner](./Glossary.md#runner) with the specified name has completed its initialization and the bootstrap sequence can continue. A faulty runner might block its own [host](./Glossary.md#host) (which in turn, for `mode=blocking` runners, could block the bootstrapping sequence). You can try to unblock (without waiting for the initialization sequence to timeout) _signaling_ the Supervisor.

```console
tw supervisor restart <NAME> [...shared...] [--confirm|-y]`
```
Restart the process associated with the runner with the specified name. Note that not all runners are restartable and this might cause the whole application to fail. Use it only as a last resort.

```console
tw supervisor claim-port <MACHINE> <NAME> [...shared...] [--confirm|-y]
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
tw runners list [search] [...shared...] [--verbose|-v] [--columns]
tw runners inspect <name> [...shared...] [--verbose|-v]
tw runners get-host <name> [...shared...]
tw runners get-name [filter] [...shared...] [--name=<filter>] [--role=<role>] [--host=<address>]
tw runners profile [filter] [...shared...]
```

### DESCRIPTION

Use `tw runners` when you want to debug, monitor and manage your runners.

### COMMANDS

```console
tw runners list [...shared...] [--filter=<filter>] [--verbose|-v] [--columns]
```
List all the runners currently active in the [system](./Glossary.md#system). Note that if a filter is specified then it's matched against the _name_ of the runners and it does not affect the [firmlets](./Glossary.md#firmlet) (which are always returned in full when `--verbose` is specified).

```console
tw runners inspect <name> [...shared...] [--verbose|-v]
```
Show detailed information about a specific runner (optionally including its firmlets). It's mostly what you can find in the [ensamble configuration file](./Ensamble.md).

```console
tw runners get-host <name> [...shared...]
```
Resolve the host name (for example `"https://my-machine:5000"`) of the specified runner. It returns an empty string if the specified runner does not host any gRPC service.

```console
tw runners get-name [search] [...shared...] [--name=<filter>] [--role=<role>] [--host=<address>]
```
Resolve the full name of a runner starting from a partial match (when using `--name`, see also `[filter]`), its role (with `--role`) or address (with `--host`). `[filter]` is the preferred alternative to `--name` when using `tw runners get-name`. If they're both specified then `[filter]` has the precedence. If more than one runner matches the expression then an error is returned.

```console
tw runners profile [search] [...shared...]
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
tw contracts list [search] [...shared...] [--host=<host>] [--verbose|-v]
tw contracts find <name> [...shared...] [--verbose|-v]
tw contracts resolve-discovery-address [...shared...]
```

### DESCRIPTION

Use `tw contracts` when you want to inspect the services registered in the [system](./Glossary.md#system).

### COMMANDS

```console
tw contracts list [search] [...shared...] [--host=<host>] [--verbose|-v]
```
List all the registered services.

```console
tw contracts find <name> [...shared...] [--verbose|-v]
```
Find the address of the service with the specified name (or family name or alias).

```console
tw contracts resolve-discovery-address [...shared...]
```
Resolve the address of the Discovery Service. You can use this if you're integrating your own runner and you want to obtain
the Discovery Service (which you can then use to discover all the other services). Calling this method is the preferred way for external code (not hosted inside one of the default hosts).

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
tw measures list [search] [...shared...] [--host=<host>] [--values] [--verbose|-v]
tw measures inspect <name> [...shared...] [--host=<host>] [--value]
tw measures read <name> [...shared...] [--host=<host>]
tw measures write <name> <value> [...shared...] [--host=<host>]
tw measures create <name> <type> <unit> [...shared...] [--host=<host>]
tw measures subscribe <name>... [...shared...] [--host=<host>]
tw measures lint <path> [--exclude=<rule>] [--strict] [--verbose]
```

### DESCRIPTION

Use `tw measures` when you want to inspect the measures registered in the [system](./Glossary.md#system) or when you want to create, read or write their values. See also [how to setup derived measures](./Derived-measures.md).

### COMMANDS

```console
tw measures list [search] [...shared...] [--host=<host>] [--values] [--verbose|-v]
```
List all the registered measures, optionally displaying their current value if `--values` is specified. Use `--verbose` to obtain all the fields associated with every measure`.

```console
tw measures inspect <name> [...shared...] [--host=<host>] [--value]
```
List some details about a specific measure. Its output is tool-friendly and can be used by a tool/program to inspect a measure's properties.

```console
tw measures read <name> [...shared...] [--host=<host>]
```
Read the current value of the measure with the specified name. Note that it does not include the unit of measure (which is the one specified when the measure has been registered). If you do not know the unit then use `tw measures list <name>`.

```console
tw measures write <name> <value> [...shared...] [--host=<host>]
```
Write a new value for the measure with the specified name. Note that you must include the unit of measure; it does not need to be the same one registered for the measure (the system will perform a conversion) but it must be compatible.

```console
tw measures create <name> <type> <unit> [...shared...] [--host=<host>]
```
Create a new measure with the specified name, type (for example `"Temperature"`) and unit (for example `"DegreesCelsius"`). See the list of [supported units](./Units.md).

```console
tw measures subscribe <name>... [...shared...] [--host=<host>]
```
Subscribe for changes to one or more measures. Each time a measure changes a new line will be printed in the form `name=value`. Note that the unit of measure is not included (use `tw measures list <name>` if you want to query it). The first value(s) printed are the current value(s) and then after each change.

```console
tw measures lint <path> [--exclude=<rule>]... [--strict] [--verbose]
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

**`<path>`**

Path of a configuration file to analyze.

**`--values`**, **`-value`**

Include the measure current value in the output.

**`--exclude=<rule>`** **`-x=<rule>`**

Exclude the specified rule from the output. It's useful when `tw measures lint` is part of a build process and a non zero return value might stop the build/deployment. If you're absolutely sure that the flagged issue is not a problem then you can instruct the linter to exclude it. Use rule ID, not name! Use `--verbose` to see exactly which rules are applied.
You can either exclude a specific rule using its ID or a group, using their category (use the `--verbose` flag to list all the applied rules, including their category).

**`--strict`**

Apply stricter rules and the exit code is not zero when all the issues are minor. Use `--verbose` to see exactly which rules are applied.

**`--verbose`** **`-v`**

Produce a detailed description of each measure.

---

##  `tw events`

Manage [events](./Glossary.md#event).

### SYNOPSIS

```console
tw events publish <topic> <subject> <verb> <object> [...shared...] [--host=<host>] [--payload=<payload>] [--correlation-id=<correlation id>]
tw events subscribe <topic> [...shared...] [--host=<host>] [--subject=<subject>] [--verb=<verb>] [--object=<object>] [--verbose]
```

### DESCRIPTION

Use `tw events` when you want to publish an event or subscribe to an event stream.

### COMMANDS

```console
tw events publish <topic> <subject> <verb> <object> [...shared...] [--host=<host>] [--payload=<payload>] [--correlation-id=<id>]
```
Publish an event.

```console
tw events subscribe <topic> [...shared...] [--host=<host>] [--subject=<subject>] [--verb=<verb>] [--object=<object>] [--verbose]
```
Subscribe to an event stream, showing only the events matching the specified filter(s).

### ARGUMENTS

**`<topic>`**

Topic of the event (for example `"signal"`).

**`<subject>`**

Subject that caused the event to be published (for example a measure `"temperature"` triggered an event with topic `"signal"` because its value triggered an alarm).

**`<verb>`**

The action performed by `<subject>` on `<object>` (for example `"triggered"` or `"created"`).

**`<object>`**

The object of the action performed by `"<subject>"` (for example `"high_temperature"` alarm, triggered by measure `"temperature"`).

**`--payload`**

Optional payload, if specified must be a valid serialized JSON object.

**`--correlation-id=<id>`**

Optional Correlation ID, if specified must be a valid UUID.

**`--subject=<subject>`**

Includes only the events with the specified subject. You can use [wildcards](#text-matching).

**`--verb=<verb>`**

Includes only the events with the specified verb. You can use [wildcards](#text-matching).

**`--object=<object>`**

Includes only the events with the specified object. You can use [wildcards](#text-matching).

**`--verbose`** **`-v`**

Produce a detailed description of each event.

---

##  `tw certs`

Manage self-signed certificates.

### SYNOPSIS

```console
tw certs create [common name] [--validity=<years>] [--export-name=<file name>] [--export-path=<path>] [--set-environment] [--export-pem]
tw certs install [path]
```

### DESCRIPTION

Use `tw certs` when you initially setup a machine to run Tinkwell and you need a certificate to use for HTTPS connection for gRPC services. If you have your own valid certificate you do not need to create a self-signed certificate, just remember to set `TINKWELL_CERT_PATH` to the path of a (valid, installed and trusted) PFX certificate. Set the password in `TINKWELL_CERT_PASS`.

### COMMANDS

```console
tw certs create [common name] [--validity=<years>] [--export-name=<file name>] [--export-path=<path>] [--set-environment] [--export-pem]
```
Create a new self-signed certificate. Remember to store the PEM files (if generated) in a secure location!
You're going to be prompted for a password.

```console
tw certs install [path]
```
Install and trust a self-signed certificate. This command is available only on Windows.
You're going to be prompted for a password.

### ARGUMENTS

**`[common name]`**

Common name `CN` (aka _Friendly name_) name for the certificate. If omitted then a generic one will be used.

**`[path]`**

Path of the certificate file to install. If omitted then the same default value used in `tw certs create` and if the file it does not exist then it'll try to read the path from `TINKWELL_CERT_PATH` environment variable.

**`--validity=<years>`**

The validity (in years) of the newly created certificate. After the certificate is expired you will need to generate (and install/trust) a new one.

**`--export-name=<file name>`**

File name (without path or extension) used to save the generated certificate. It'll be located in the directory specified with `--export-path` and the extension depends on the format.

**`--export-path=<path>`**

Directory where the generated certificate(s) will be saved.

**`--set-environment`**

Set the environment variables needed to instruct Tinkwell to use the generated certificate. If omitted then you're going to do it manually. This option is available only on Windows. Default is true if the environment variable `TINKWELL_CERT_PATH` is not already set.

**`--export-pem`**

Export also the PEM files (certificate and key) together with the PFX certificate needed to run Tinkwell. You're going to need them to install/trust the certificate if running on Linux/BSD/MacOS. Default is false for Windows.

### Examples

In a typical fresh Windows setup, all you need to do is:

```bash
./tw certs create
./tw certs install
```
---

##  `tw ensamble`

Validate ensamble configuration files.

### SYNOPSIS

```console
tw ensamble lint <path> [--strict] [--exclude<rule>] [--verbose]
```

### DESCRIPTION

Use `tw ensamble` to manage the ensamble configuration files. See also [how to setup an ensamble](./Ensamble.md).

### COMMANDS

```console
tw ensamble lint <path> [--strict] [--exclude<rule>] [--verbose]
```
Check the specified .tw (Tinkwell Ensamble configuration) file for errors or bad practices. The return code is non zero if the file contains any known issue. Use `--exclude` if you want to exclude a specific rule.

### ARGUMENTS

**`<path>`**

Path of the configuration file to lint.

**`--exclude=<rule>`** **`-x=<rule>`**

Exclude the specified rule from the output. It's useful when `tw ensamble lint` is part of a build process and a non zero return value might stop the build/deployment. If you're absolutely sure that the flagged issue is not a problem then you can instruct the linter to exclude it. Use rule ID, not name! Use `--verbose` to see exactly which rules are applied.
You can either exclude a specific rule using its ID or a group, using their category (use the `--verbose` flag to list all the applied rules, including their category).

**`--strict`**

Apply stricter rules and the exit code is not zero when all the issues are minor. Use `--verbose` to see exactly which rules are applied.

**`--verbose`** **`-v`**

Produce a detailed description of the linting process.

---

##  `tw actions`

Validate .twa actions configuration files.

### SYNOPSIS

```console
tw actions lint <path> [--strict] [--exclude<rule>] [--verbose]
```

### DESCRIPTION

Use `tw actions` to manage the .twa actions configuration files. See also [how to configure actions](./Actions.md).

### COMMANDS

```console
tw ensamble actions <path> [--strict] [--exclude<rule>] [--verbose]
```
Check the specified .twa (Tinkwell Actions configuration) file for errors or bad practices. The return code is non zero if the file contains any known issue. Use `--exclude` if you want to exclude a specific rule.

### ARGUMENTS

**`<path>`**

Path of the configuration file to lint.

**`--exclude=<rule>`** **`-x=<rule>`**

Exclude the specified rule from the output. It's useful when `tw actions lint` is part of a build process and a non zero return value might stop the build/deployment. If you're absolutely sure that the flagged issue is not a problem then you can instruct the linter to exclude it. Use rule ID, not name! Use `--verbose` to see exactly which rules are applied.
You can either exclude a specific rule using its ID or a group, using their category (use the `--verbose` flag to list all the applied rules, including their category).

**`--strict`**

Apply stricter rules and the exit code is not zero when all the issues are minor. Use `--verbose` to see exactly which rules are applied.

**`--verbose`** **`-v`**

Produce a detailed description of the linting process.

---


##  `tw mqtt`

Debug the MQTT bridge.

### SYNOPSIS

```console
tw mqtt send <topic> <message> [--address=<address>] [--port=<port>] [--client-id=<id>] [--username=<username>]
```

### DESCRIPTION

Use `tw mqtt` to help debugging/testing the [MQTT Bridge](./MQTT-Bridge.md).

### COMMANDS

```console
tw mqtt send <topic> <message> [--address=<address>] [--port=<port>] [--client-id=<id>] [--username=<username>]
```
Send the specified message to the specified topic. All the default options match with a default setup of the [internal MQTT Broker](./MQTT-Server.md).

### ARGUMENTS

**`<topic>`**

Topic of the message.

**`<message>`**

Message to send.

**`--address=<address>`**

Address where the MQTT Broker is listening. Default is `localhost`.

**`--port=<port>`**

Port where the MQTT Broker is listening. Default is `1883`.

**`--client-id=<id>`**

ID of the client used to connect to the broker. Default is `TinkwellCliMqttClient`.

**`--username=<username>`**

If specified then the connection requires credentials. `tw` will look for the password in the environment variable `TINKWELL_MQTT_PASSWORD` and if not present then it'll prompt the user for a password.
