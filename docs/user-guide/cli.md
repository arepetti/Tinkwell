# tw(1) — Tinkwell CLI Reference

## SYNOPSIS

```
tw [OPTIONS] <command> [<args>]
```

## DESCRIPTION

`tw` is the command-line interface for managing and inspecting a running Tinkwell coordinator.
It communicates with the coordinator over named pipes and with individual services over gRPC.

## GLOBAL OPTIONS

These options are available on every command.

**-p, --pipe** _name_ :   Coordinator pipe name.
Default: `tinkwell-coordinator`.

**-m, --machine** _host_ :   Remote machine for named pipe connection.
Default: `.` (localhost).

**-f, --format** _format_ :   Output format: `table`, `list`, or `jsonl`.
Default depends on the command (usually `table` for interactive, `jsonl` for non-interactive).

**-v, --verbose** :   Show additional columns or properties in output.

**-n, --non-interactive** :   Disable colors, prompts, and progress indicators.
Forces `jsonl` output.
Useful for scripting.

---

## INIT (GUIDED GENERATOR)

### tw init

Generate configuration files from a wizard pack.
Walks through an interactive questionnaire and renders output files from Liquid templates.

```
tw init [--pack|-p <name>] [--output|-o <path>] [--force] [--dry-run]
        [--list-packs] [--pack-path <dir>]
```

**--pack, -p** _name_ :   Wizard pack name or directory.
Auto-selected when only one pack exists.

**--output, -o** _path_ :   Override the primary output file path.

**--force** :   Overwrite existing files without prompting.

**--dry-run** :   Preview generated files without writing them.

**--list-packs** :   List available packs and exit.

**--pack-path** _dir_ :   Additional directory to search for packs.

The default `tinkwell-ensemble` pack generates a complete `ensemble.tw` file with configurable topology, services, protocols, and data routing.

See [Wizard packs reference](../reference/init-packs.md) for pack authoring.

## LIFECYCLE COMMANDS

### tw start

Start the coordinator.

```
tw start [<config>] [-B]
```

**\<config\>** :   Path to the ensemble `.tw` configuration file.
Optional if the coordinator has a default.

**-B, --background** :   Detach and run in background.

The coordinator process is launched from the same directory as the `tw` executable.
The `--pipe` name is forwarded to the coordinator.

---

### tw quit

Shut down the coordinator gracefully.

```
tw quit [-w]
```

**-w, --wait** :   Wait until the coordinator has fully shut down (polls for up to ~60 seconds).

---

### tw ping

Check if the coordinator is reachable.

```
tw ping
```

Reports reachability and round-trip latency.
Exits with a non-zero code if the coordinator is unreachable.

---

### tw status

Show a summary of the coordinator and runners.

```
tw status
```

Contacts the coordinator and displays:
- **Coordinator** — reachability and latency
- **Runners** — total count with breakdown by status (ready, starting, crashed, etc.)

Exits with 0 if the coordinator is reachable, 1 otherwise.

**JSONL output** (`--non-interactive` or `--format jsonl`):

```json
{
  "coordinator": { "reachable": true, "latencyMs": 12, "error": null },
  "runners": { "total": 3, "byStatus": { "ready": 2, "starting": 1 } }
}
```

---

### tw info

Show local system information.
Does not contact the coordinator.

```
tw info [-v]
```

Displays:
- **Product version** — from the assembly's informational version
- **.NET runtime** — runtime version
- **OS** — operating system description
- **Architecture** — process architecture (x64, arm64, etc.)
- **App directory** — `AppContext.BaseDirectory`

With `--verbose`:
- **Plugin roots** — directories searched for plugins (with existence check)
- **Extensions** — loaded command extension DLLs

**JSONL output** (`--non-interactive` or `--format jsonl`):

```json
{
  "productVersion": "0.1.0",
  "runtime": "10.0.0",
  "os": "Microsoft Windows 10.0.26100",
  "architecture": "X64",
  "baseDirectory": "C:\\tools\\tinkwell\\"
}
```

With `--verbose`, `pluginRoots` and `extensions` are included:

```json
{
  "productVersion": "0.1.0",
  "runtime": "10.0.0",
  "os": "Microsoft Windows 10.0.26100",
  "architecture": "X64",
  "baseDirectory": "C:\\tools\\tinkwell\\",
  "pluginRoots": ["C:\\Users\\me\\AppData\\Local\\tinkwell\\plugins"],
  "extensions": ["Tinkwell.Cli.Commands.Mqtt"]
}
```

---

### tw unblock

Unblock runners waiting in the startup sequence.

```
tw unblock
```

Sends `notify unblock` to the coordinator.
Useful when a runner is stuck during initialization and you want the startup sequence to proceed.

---

### tw raw

Send a raw pipe command to the coordinator.

```
tw raw <command> [-y]
```

**\<command\>** :   The command string to send (e.g. `"service list"`, `"runners list"`).

**-y, --no-confirm** :   Skip the confirmation prompt.

Prints the raw JSON response.
Intended for debugging and advanced use.

---

### tw run

Execute a batch script file.

```
tw run <file> [--echo]
```

**\<file\>** :   Path to a text file containing one `tw` command per line.

**--echo** :   Print each command before executing it.

Blank lines and lines starting with `#` are skipped.
The global options `--pipe`, `--machine`, `--non-interactive`, and `--verbose` are inherited by each command.
Execution stops on the first non-zero exit code.

---

## RUNNERS

### tw runners list

List all runners in the ensemble.

```
tw runners list
```

Shows: Name, ID, Status, PID, Startup Time, Endpoint.

---

### tw runners health

Show health information for all runners.

```
tw runners health
```

Shows: Runner, Status, CPU%, Memory, Threads, Handles, Health Checks, Last Updated.
Health data is read from the `_health` bucket in the state store.

---

## SERVICES

### tw services find

Find a registered service by name, alias, or family.

```
tw services find <name>
```

**\<name\>** :   Service name, alias, or family name to look up.

Shows the matching service's name, type, host, URL, family, and aliases.

---

### tw services list

List all registered services.

```
tw services list [-q <query>]
```

**-q, --query** _text_ :   Filter by name, alias, or family.

---

## STORE

Commands for the key-value state store.
Requires a running store runlet.

### tw store get

Retrieve a value from the store.

```
tw store get <key> -b <bucket> [-s <namespace>]
```

**\<key\>** :   The key to retrieve.

**-b, --bucket-id** _id_ :   Bucket ID (required).

**-s, --namespace** _ns_ :   Key namespace.

---

### tw store set

Set a value in the store.

```
tw store set <key> <value> -b <bucket> [-s <namespace>] [-t <ttl>]
```

**\<key\>** :   The key to set.

**\<value\>** :   JSON value to store.

**-b, --bucket-id** _id_ :   Bucket ID (required).

**-s, --namespace** _ns_ :   Key namespace.

**-t, --ttl** _seconds_ :   Time-to-live in seconds.
`0` means permanent.
Default: `0`.

---

### tw store delete

Delete a key from the store.

```
tw store delete <key> -b <bucket> [-s <namespace>]
```

**\<key\>** :   The key to delete.

**-b, --bucket-id** _id_ :   Bucket ID (required).

**-s, --namespace** _ns_ :   Key namespace.

---

### tw store list

List entries in the store.

```
tw store list [-b <bucket>] [-s <namespace>] [--prefix <prefix>] [-a]
```

**-b, --bucket-id** _id_ :   Bucket ID.
If omitted, lists across all discoverable buckets.

**-s, --namespace** _ns_ :   Key namespace.

**--prefix** _text_ :   Filter keys by prefix.

**-a, --all** :   Include hidden (non-discoverable) buckets.
Default: `true`.

---

### tw store watch

Watch for store changes in real time.

```
tw store watch [-b <bucket>] [-s <namespace>] [--prefix <prefix>]
```

Streams SET, DELETE, and EXPIRED events until interrupted with Ctrl+C.

**-b, --bucket-id** _id_ :   Bucket ID.

**-s, --namespace** _ns_ :   Key namespace.

**--prefix** _text_ :   Filter keys by prefix.

---

## MEASURES

Commands for the measures registry.
Requires a running measures runlet.

### tw measures list

List all registered measures.

```
tw measures list [-c <category>]
```

**-c, --category** _name_ :   Filter by category.

Shows: Name, Value, Unit, Category, and additional columns with `--verbose`.

---

### tw measures get

Get a single measure's current value and definition.

```
tw measures get <name>
```

**\<name\>** :   Measure name.

---

### tw measures set

Update a measure's value.

```
tw measures set <name> <value>
```

**\<name\>** :   Measure name.

**\<value\>** :   New value (number or string).

---

### tw measures register

Register a new measure definition.

```
tw measures register <name> [options]
```

**\<name\>** :   Measure name.

**-t, --type** _type_ :   `Number` or `String`.
Default: `Number`.

**--quantity** _name_ :   UnitsNet quantity type (e.g. `Temperature`, `ElectricPotential`).
Default: `Scalar`.

**-u, --unit** _name_ :   Unit name (e.g. `DegreeCelsius`, `Volt`).

**--min** _value_ :   Minimum allowed value.

**--max** _value_ :   Maximum allowed value.

**--precision** _digits_ :   Decimal places for rounding.

**--ttl** _seconds_ :   Time-to-live in seconds.
`0` means no expiration.
Default: `0`.

**-c, --category** _name_ :   Grouping category.

**-d, --description** _text_ :   Human-readable description.

---

### tw measures watch

Watch for measure value changes.

```
tw measures watch
```

Streams value changes until interrupted with Ctrl+C.
With `--verbose`, includes the value type and previous value.

---

## SIGNALS

Commands for the signals system.
Requires a running signals runlet.

### tw signals create

Create a new signal definition at runtime.

```
tw signals create <name> -w <when> [-u <until>] [--for <duration>] [-s key=value...]
```

**\<name\>** :   Signal name.

**-w, --when** _expression_ :   Trigger condition (required).
A boolean expression referencing measure names.

**-u, --until** _expression_ :   Deactivation condition (hysteresis).

**--for** _duration_ :   Duration the `when` condition must hold before firing.
Accepts seconds, a duration string (e.g. `"5s"`), or an expression.

**-s, --set** _key=value_ :   Additional signal properties.
Repeatable.

---

### tw signals list

List all signal definitions.

```
tw signals list
```

Shows: Name, When expression, Until expression, For duration, Parent measure, Properties.

---

### tw signals watch

Watch for signal events.

```
tw signals watch [--beep]
```

Streams fired signal events until interrupted with Ctrl+C.
With `--verbose`, includes timestamp and signal properties.

**--beep** :   Emit an audible terminal bell (`BEL`) on each signal event.
Works on all platforms.

---

## EVENTS

Commands for the event bus.
Requires a running events runlet.

### tw events watch

Subscribe to and stream events.

```
tw events watch [-s <source>] [--verb <verb>...] [--name <prefix>]
```

**-s, --source** _name_ :   Filter by source (e.g. `signals`, `measures`, `cli`).

**--verb** _verb_ :   Filter by verb (e.g. `Fired`, `Changed`).
Repeatable.

**--name** _prefix_ :   Filter by event name prefix.

Streams events until interrupted with Ctrl+C.

---

### tw events publish

Publish an event to the event bus.

```
tw events publish <name> [options]
```

**\<name\>** :   Event name.

**--verb** _verb_ :   Event verb: `Fired`, `Changed`, `Created`, `Deleted`, `Expired`, `Started`, `Stopped`, `Failed`, or a custom string.
Default: `Custom`.

**-s, --source** _name_ :   Source identifier.
Default: `cli`.

**-o, --object** _text_ :   Object/value string.

**--set** _key=value_ :   Extra payload entries.
Repeatable.

**--correlation-id** _id_ :   Correlation ID.
Auto-generated if omitted.

---

## MQTT

Testing commands for MQTT brokers.
These connect directly to a broker — they do not go through the Tinkwell MQTT runlet.

### tw mqtt ping

Test connectivity to an MQTT broker.

```
tw mqtt ping [-b <broker>] [--port <port>] [--client-id <id>]
```

**-b, --broker** _host_ :   Broker hostname.
Default: `localhost`.

**--port** _port_ :   Broker port.
Default: `1883`.

**--client-id** _id_ :   Client ID.
Auto-generated if omitted.

Reports connection latency.
With `--verbose`, includes session-present flag.

---

### tw mqtt publish

Publish a message to an MQTT broker.

```
tw mqtt publish <topic> <payload> [-b <broker>] [--port <port>] [-q <qos>] [--retain] [--client-id <id>]
```

**\<topic\>** :   MQTT topic.

**\<payload\>** :   Message payload.

**-b, --broker** _host_ :   Broker hostname.
Default: `localhost`.

**--port** _port_ :   Broker port.
Default: `1883`.

**-q, --qos** _level_ :   Quality of service: `0`, `1`, or `2`.
Default: `0`.

**--retain** :   Retain the message on the broker.

**--client-id** _id_ :   Client ID.
Auto-generated if omitted.

---

### tw mqtt start-broker

Start a lightweight development MQTT broker.
This runs a standalone broker process without the full Tinkwell runtime — useful for local testing and development.
Press Ctrl+C to stop.

```
tw mqtt start-broker [--port <port>]
```

**--port** _port_ :   TCP port to listen on.
Default: `1883`.

The broker accepts all connections without authentication and does not persist messages.
Client connect/disconnect events are printed to the console.

---

## COAP

CoAP client and server tools for testing.
Send raw CoAP requests, or start a development server with heartbeat mailbox support.

### tw coap send

Send a CoAP request.

```
tw coap send <method> <path> [-H <host>] [--port <port>] [-d <payload>] [-a <format>] [-t <timeout>]
```

**\<method\>** :   Request method: `get`, `post`, `put`, or `delete`.

**\<path\>** :   URI path (e.g. `/sensors/temperature`).

**-H, --host** _host_ :   Target host.
Default: `localhost`.

**--port** _port_ :   UDP port.
Default: `5683`.

**-d, --payload** _data_ :   Request payload (for POST/PUT).

**-a, --accept** _format_ :   Accepted response format: `text`, `binary`, or `json`.

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

Shows the response code and payload.
Binary payloads are decoded as float32.

---

### tw coap server

Start a development CoAP server with optional heartbeat mailbox support.
Useful for simulating a hub during device development.
Press Ctrl+C to stop.
Mailbox mode uses **protobuf** for heartbeat replies, device payloads, and hub-push command dispatch; decoded messages are shown on the console as JSON.

```
tw coap server [--port <port>] [--bind <addr>] [--path <mapping>...]
               [--mailbox <path>] [--prefix <name>] [--queue <spec>...]
               [--log-payload]
```

**--port** _port_ :   UDP port to listen on.
Default: `5684`.

**--bind** _address_ :   Bind address.
Default: `0.0.0.0`.

**--path** _mapping_ :   Fixed response mapping in format `/uri-path=response-body`.
Repeatable.
The first `=` after the leading `/` is the delimiter.
Responds to any method with 2.05 Content (text/plain).
If no `=` is present, responds with an empty 2.05.

**--mailbox** _path_ :   Designate a path as a heartbeat mailbox endpoint (e.g. `/hub/heartbeat`).
On each device POST, the server responds with a **HeartbeatReply** protobuf containing only the **pending** command count.
Queued commands are then sent to the device as **individual CoAP POST** requests (FIFO), not inlined in the heartbeat response.
GETs peek at the queue count without draining.
Incoming device heartbeat bodies are decoded from protobuf and displayed as JSON.

**--prefix** _name_ :   CoAP path prefix used for hub-push dispatch to the device (e.g. resource paths under `/<prefix>/...`).
Default: `tw`.

**--queue** _spec_ :   Pre-queue a hub command before startup.
Repeatable.
Format is `command` or `command:json` — a command name, optionally followed by `:` and a JSON payload for that command.
Examples: `reboot`, `set-config:{"entries":[{"key":"mode","value":"cool"}]}`.
Commands are consumed FIFO when the device posts to the mailbox path.

**--log-payload** :   Print incoming request payloads for all endpoints.

When `--mailbox` is set, a handler for **`/hub/telemetry`** is registered automatically.
Telemetry POST bodies are decoded from protobuf and displayed as JSON.

When `--mailbox` is set and the command is interactive, stdin lines are read and added to the command queue in real-time, using the same **`command[:json]`** format as `--queue`.

If neither `--path` nor `--mailbox` is provided, the server runs in **echo mode**: any request receives a 2.05 response containing the method and path.

**Examples:**

```bash
# Simulate a hub: heartbeats, protobuf dispatch
tw coap server --port 5684 --mailbox /hub/heartbeat --queue reboot
tw coap server --port 5684 --mailbox /hub/heartbeat \
    --queue 'set-config:{"entries":[{"key":"mode","value":"cool"}]}'

# Custom hub-push path prefix (default is tw)
tw coap server --port 5684 --mailbox /hub/heartbeat --prefix myapp --queue reboot

# Hub with additional fixed response paths
tw coap server --port 5684 --mailbox /hub/heartbeat \
    --path "/tw/info=vendor-id=42\nproduct-id=1"

# General-purpose test server with fixed responses
tw coap server --port 5683 --path "/sensor/temp=23.5" --path "/health=ok"

# Echo mode (no arguments beyond port)
tw coap server --port 5683
```

On exit, prints a summary: `N heartbeat(s) received, M command(s) dispatched`.

---

## MODBUS

Test and debug Modbus RTU/TCP devices.

### tw modbus read

Read one or more registers from a Modbus device.

```
tw modbus read <address> [options]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--transport`, `-t` | `tcp` | Transport: `rtu` or `tcp` |
| `--host` | `localhost` | TCP host |
| `--tcp-port` | `502` | TCP port |
| `--port` | — | Serial port (RTU, e.g. `/dev/ttyUSB0`, `COM3`) |
| `--baudrate` | `9600` | Serial baud rate (RTU) |
| `--slave`, `-s` | `1` | Modbus slave ID |
| `--count`, `-c` | `1` | Number of registers to read |
| `--type` | — | Decode type (`int16`, `float32-be`, etc.). If omitted, raw hex values are shown. |
| `--scale` | `1.0` | Scale factor applied to the decoded value |
| `--input` | `false` | Read input registers (FC 04) instead of holding (FC 03) |
| `--output` | `table` | `table` or `jsonl` |

Examples:

```bash
# Read 2 holding registers as a float32 from a TCP device
tw modbus read 0x0000 --count 2 --type float32-be --host 192.168.1.100

# Read 1 input register via RTU
tw modbus read 0x0010 --transport rtu --port COM3 --slave 1 --input --type int16 --scale 0.1

# Raw register dump (no decode)
tw modbus read 0x0000 --count 4 --host 192.168.1.100
```

JSONL output (`--output jsonl`):

```json
{"address":"0x0000","type":"float32-be","value":42.5,"raw":["0x4229","0x0000"]}
```

### tw modbus write

Write a single holding register (FC 06).

```
tw modbus write <address> <value> [options]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--transport`, `-t` | `tcp` | Transport: `rtu` or `tcp` |
| `--host` | `localhost` | TCP host |
| `--tcp-port` | `502` | TCP port |
| `--port` | — | Serial port (RTU) |
| `--baudrate` | `9600` | Baud rate (RTU) |
| `--slave`, `-s` | `1` | Modbus slave ID |
| `--output` | `table` | `table` or `jsonl` |

Example:

```bash
tw modbus write 0x0064 1234 --host 192.168.1.100
```

JSONL output:

```json
{"status":"ok","address":"0x0064","value":1234}
```

---

## LWM2M

Commands for LwM2M device management.
These send CoAP requests to a LwM2M server (e.g. for device simulation and testing).
They do not go through the coordinator.

### tw lwm2m register

Register a virtual device with a LwM2M server.

```
tw lwm2m register <endpoint> <objects> [-H <host>] [--port <port>] [-l <lifetime>] [-t <timeout>]
```

**\<endpoint\>** :   Client endpoint name (e.g. `my-sensor`).

**\<objects\>** :   Comma-separated object paths to register (e.g. `3/0,3303/0,3304/0`).

**-H, --host** _host_ :   LwM2M server host.
Default: `localhost`.

**--port** _port_ :   CoAP port.
Default: `5683`.

**-l, --lifetime** _seconds_ :   Registration lifetime.
Default: `300`.

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

Sends a CoAP POST to `/rd` with a link-format payload.
On success, reports the registration location.

---

### tw lwm2m update

Update an existing LwM2M registration.

```
tw lwm2m update <location> [-H <host>] [--port <port>] [-l <lifetime>] [-o <objects>] [-t <timeout>]
```

**\<location\>** :   Registration location path returned by `register` (e.g. `rd/abc123`).

**-H, --host** _host_ :   LwM2M server host.
Default: `localhost`.

**--port** _port_ :   CoAP port.
Default: `5683`.

**-l, --lifetime** _seconds_ :   Updated lifetime (optional).

**-o, --objects** _paths_ :   Updated comma-separated object paths (optional).

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

---

### tw lwm2m deregister

Remove a client registration from the LwM2M server.

```
tw lwm2m deregister <location> [-H <host>] [--port <port>] [-t <timeout>]
```

**\<location\>** :   Registration location path (e.g. `rd/abc123`).

**-H, --host** _host_ :   LwM2M server host.
Default: `localhost`.

**--port** _port_ :   CoAP port.
Default: `5683`.

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

Sends a CoAP DELETE to the registration location.

---

### tw lwm2m read

Read a resource value from a LwM2M device.

```
tw lwm2m read <path> [-H <host>] [--port <port>] [-a <format>] [-t <timeout>]
```

**\<path\>** :   Resource path (e.g. `/3303/0/5700` for a temperature sensor value).

**-H, --host** _host_ :   Target host.
Default: `localhost`.

**--port** _port_ :   CoAP port.
Default: `5683`.

**-a, --accept** _format_ :   Preferred response format: `text`, `tlv`, or `json`.

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

---

### tw lwm2m write

Write a value to a LwM2M resource.

```
tw lwm2m write <path> <payload> [-H <host>] [--port <port>] [-t <timeout>]
```

**\<path\>** :   Resource path (e.g. `/3303/0/5700`).

**\<payload\>** :   Value to write (text/plain).

**-H, --host** _host_ :   Target host.
Default: `localhost`.

**--port** _port_ :   CoAP port.
Default: `5683`.

**-t, --timeout** _seconds_ :   Response timeout.
Default: `5`.

Sends a CoAP PUT with a `text/plain` payload.

---

## PACKAGE

Commands for creating, inspecting, and verifying Tinkwell packages.
These operate locally and do not require a running coordinator.

### tw package create-manifest

Create a `package.tw` manifest file interactively or from command-line arguments.

```
tw package create-manifest [output] [--set name=value ...]
```

**\<output\>** :   Output file path or directory.
When a directory is given, creates `package.tw` inside it.
Defaults to `package.tw` in the current directory.

**-s, --set** _name=value_ :   Set a manifest property directly.
Repeatable.
When present, interactive prompts are skipped and only the provided values are used.
Accepts both known keys (`name`, `version`, `author`, etc.) and custom keys.

In **interactive mode** (default) the command prompts for each known property in order.
Press Enter to skip optional fields.
In **non-interactive mode** (`--non-interactive`) without `--set`, it reads one value per line from stdin (in the same order), then reads additional `key=value` lines until an empty line or EOF.

**Examples:**

```
# Interactive — prompts for each field
tw package create-manifest my-pkg/

# From arguments — no prompts
tw package create-manifest my-pkg/ \
  --set name=my-plugin --set version=1.0.0 \
  --set author="Jane Doe" --set license=MIT

# Non-interactive from stdin
echo -e "my-plugin\n1.0.0\nJane Doe\n\n\n\n\n\n\nMIT\n" | \
  tw package create-manifest my-pkg/ --non-interactive
```

---

### tw package pack

Pack a directory into a signed Tinkwell package.

```
tw package pack <source> <output> [-k <key>] [--no-sign]
                                  [--from-content [-m <manifest>]]
```

**\<source\>** :   Source directory.
By default must contain `package.tw` and a `content/` subdirectory.
When `--from-content` is used, this is the content directory itself (e.g. a `dotnet publish` output).

**\<output\>** :   Output `.zip` file path.

**-k, --key** _path_ :   Path to the PKCS#8 private key file for signing.

**--no-sign** :   Create the package without signatures.

**--from-content** :   Treat `<source>` as the raw content directory.
No `package.tw` or `content/` subfolder is needed; the command builds the package structure automatically.
When this flag is set, the manifest is resolved from `--manifest` or by interactive prompts (see below).

**-m, --manifest** _path_ :   Path to an existing `package.tw` manifest file.
Only used with `--from-content`.
If `--from-content` is specified without `--manifest`, the command prompts for manifest fields interactively.
In non-interactive mode (`--non-interactive`), `--manifest` is required.

**Examples:**

```
# Standard pack from a prepared package directory
tw package pack ./my-package my-package.zip --key publisher.key

# Pack directly from a content directory with an existing manifest
tw package pack ./publish-output my-plugin-1.0.0.zip --key publisher.key \
  --from-content --manifest my-plugin-manifest.tw

# Pack from a content directory with interactive manifest prompts
tw package pack ./publish-output my-plugin-1.0.0.zip --key publisher.key \
  --from-content
```

---

### tw package unpack

Unpack a Tinkwell package to a directory.

```
tw package unpack <package> <output> [-k <key>] [--no-verify] [--allow-unsigned]
```

**\<package\>** :   Package `.zip` file to unpack.

**\<output\>** :   Output directory.

**-k, --key** _path_ :   Path to the publisher's public key file for signature verification.

**--no-verify** :   Skip integrity verification entirely.

**--allow-unsigned** :   Accept packages that have no signatures.

**Example:**

```
tw package unpack my-package.zip ./output --key publisher.pub
```

---

### tw package verify

Verify a Tinkwell package's integrity and signatures.

```
tw package verify <path> [-k <key>] [--allow-unsigned]
```

**\<path\>** :   Path to a `.zip` package, a package root directory, or a `package.tw` file.

**-k, --key** _path_ :   Path to the publisher's public key file.

**--allow-unsigned** :   Accept packages that have no signatures.

Reports each verification issue with its code and severity.
Exits `0` when the package is valid.

**Example:**

```
tw package verify my-package.zip --key publisher.pub
```

---

### tw package resign

Re-sign an existing Tinkwell package with a new key.

```
tw package resign <input> <output> -k <key>
```

**\<input\>** :   Source package `.zip` file.

**\<output\>** :   Output `.zip` file with new signatures.

**-k, --key** _path_ :   Path to the new PKCS#8 private key file.

The content is preserved; only the `security/` directory is regenerated.

**Example:**

```
tw package resign old.zip new.zip --key new-publisher.key
```

---

## IDENTITY

Commands for generating signing keys and managing identity across Tinkwell services.
These commands are service-agnostic and require explicit `--url` when communicating with a remote service.

### tw identity generate-key

Generate an ECDSA P-384 key pair for signing and identity.

```
tw identity generate-key <private-key> <public-key> [--force]
```

**\<private-key\>** :   Output path for the private key file (PKCS#8 binary).

**\<public-key\>** :   Output path for the public key file (X.509 SubjectPublicKeyInfo binary).

**--force** :   Overwrite existing files.
Without this flag the command aborts if either file already exists.

**Example:**

```
tw identity generate-key publisher.key publisher.pub
```

---

### tw identity signup

Register a new author account on a Tinkwell service.

```
tw identity signup --url <service-url> [--handle <handle>] [--public-name <name>]
                   [--email <email>] [--company <company>] [--author-key <path>]
                   [--timeout <seconds>]
```

All parameters are specified on the command line.
In interactive mode, missing fields are prompted and a confirmation is shown before submission.

**--url** *(required)* :   Base URL of the target service (e.g. `https://registry.example.com`).

**--timeout** :   HTTP timeout in seconds (default: 60).

**Example:**

```
tw identity signup --url https://plugins.tinkwell.io \
  --handle arepetti --public-name "Adriano Repetti" \
  --email adriano@example.com --author-key publisher.pub
```

---

### tw identity rotate-key

Rotate your API key on a Tinkwell service.
The current key is immediately invalidated.

```
tw identity rotate-key --url <service-url> --api-key <key> [--timeout <seconds>]
```

In interactive mode a confirmation prompt is shown before proceeding.
The new API key is displayed once and must be saved immediately.

**--url** *(required)* :   Base URL of the target service.

**--api-key** *(required)* :   Current API key for authentication.

**--timeout** :   HTTP timeout in seconds (default: 60).

**Example:**

```
tw identity rotate-key --url https://plugins.tinkwell.io --api-key <current-key>
```

---

### tw identity delete-account

Soft-delete your account on a Tinkwell service.
This is irreversible: your published content is soft-deleted per service policy.
In interactive mode, a warning and confirmation are shown before the request; use `--non-interactive` to skip prompts (still requires `--url` and `--api-key`).

```
tw identity delete-account --url <service-url> --api-key <key> [--timeout <seconds>]
```

**--url** *(required)* :   Base URL of the target service.

**--api-key** *(required)* :   Current API key for authentication.

**--timeout** :   HTTP timeout in seconds (default: 60).

**Example:**

```
tw identity delete-account --url https://plugins.tinkwell.io --api-key <current-key>
```

---

## PLUGIN

Commands for managing Tinkwell plugins.
Plugins are installed to the user's local application data directory and are available to all Tinkwell components.
These commands operate locally and do not require a running coordinator.

### tw plugin install

Install a plugin from a package file, URL, the plugin registry, or GitHub Releases.

```
tw plugin install <source> [-k <key>] [--force] [--update] [--allow-unsigned]
                           [--registry-url <url>] [--registry-key <path>]
                           [--github-repo <owner/repo>]
```

**\<source\>** :   Package source.
Can be:
    - A **local file path** or UNC network path to a `.zip` package.
    - An **HTTP(S) URL** to download a `.zip` package.
    - A **package name** in the format `handle/plugin-name` or `handle/plugin-name@version`.
      The command tries the registry first; if no registry URL is configured (or the package is not found there), it falls back to GitHub Releases.
      When a version is omitted, the latest version matching the current platform is selected.

**-k, --key** _path_ :   Path to the publisher's public key file for signature verification.

**--force** :   Overwrite if the same version is already installed.

**--update** :   After installing, remove all older versions of the same plugin.

**--allow-unsigned** :   Allow packages without signatures.

**--registry-url** _url_ :   Plugin registry URL.
Overrides the `TW_REGISTRY_URL` environment variable and the `url` field in `registry.json`.

**--registry-key** _path_ :   Path to the registry's public key file for signature verification of registry-downloaded packages.
Overrides the `TW_REGISTRY_PUBLIC_KEY_FILE` environment variable and the `publicKeyFile` field in `registry.json`.

**--github-repo** _owner/repo_ :   GitHub repository to use as a fallback plugin source.
Overrides the `TW_GITHUB_REPO` environment variable and the `githubRepo` field in `registry.json`.
Default: `arepetti/tinkwell-static-plugins-registry`.

The package must contain a `package.tw` with both `name` and `version` fields.
The plugin is installed to `{LocalApplicationData}/Tinkwell/plugins/{name}@{version}/`.

If the manifest contains a `product-version` custom property, the installer checks it against the current Tinkwell version.
The value uses [NuGet version range syntax](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#version-ranges) (e.g. `[0.1, 1.0)` for >= 0.1.0 and < 1.0.0).
When the versions are incompatible, installation is rejected unless `--force` is specified.

**Registry resolution:** When `<source>` is a package name (e.g. `arepetti/sensor-driver`), the command first queries the registry API (if a registry URL is configured).
It automatically detects the current platform architecture (e.g. `Windows_x64`, `Linux_arm64`) and falls back to the platform-agnostic variant (e.g. `Windows`, `Linux`) if no architecture-specific build is available.
The registry URL is resolved from (highest priority first): `--registry-url`, `TW_REGISTRY_URL` environment variable, `{LocalApplicationData}/Tinkwell/registry.json`.

**GitHub fallback:** If no registry URL is configured, or the package is not found in the registry, the command falls back to GitHub Releases.
It looks for a release tagged `{plugin-name}@{version}` in the configured GitHub repository.
Assets are matched by name using the convention `{plugin-name}-{version}.zip` (for platform-agnostic/AnyCPU plugins) or `{plugin-name}-{version}-{architecture}.zip` (for the rare platform-specific plugin).
The GitHub repo is resolved from (highest priority first): `--github-repo`, `TW_GITHUB_REPO` environment variable, `githubRepo` in `registry.json`, default `arepetti/tinkwell-static-plugins-registry`.

**Examples:**

```
# Install from a local file
tw plugin install my-runlet-json-1.0.0.zip --key publisher.pub

# Install from a URL and remove older versions
tw plugin install https://example.com/plugins/sensor-1.2.0.zip --update

# Install from the registry (latest version, auto-detect architecture)
tw plugin install arepetti/sensor-driver

# Install a specific version from the registry
tw plugin install arepetti/sensor-driver@2.0.0

# Install from the registry with an explicit URL
tw plugin install arepetti/sensor-driver --registry-url https://plugins.tinkwell.io

# Install from GitHub (no registry configured, uses default repo)
tw plugin install arepetti/sensor-driver

# Install from a specific GitHub repo
tw plugin install arepetti/sensor-driver --github-repo myorg/my-plugins

# Install unsigned package (development)
tw plugin install ./dev-build.zip --allow-unsigned --force

# Force install despite version incompatibility
tw plugin install old-plugin.zip --force
```

---

### tw plugin search

Search the plugin registry for available packages.

```
tw plugin search [--filter <expr>] [--sort <fields>] [--page-size <n>] [--all]
                 [--registry-url <url>]
```

**--filter** _expr_ :   Filter expression using the registry DSL.
Multiple terms are comma-separated and AND-ed.
`==` for exact match (case-insensitive), `~` for contains (case-insensitive).
Example: `name~sensor,architecture==Linux_x64`.

**--sort** _fields_ :   Comma-separated sort fields.
Prefix `-` for descending.
Example: `-publishDate,name`.

**--page-size** _n_ :   Results per page (1-100).
Default: `20`.

**--all** :   Auto-paginate through all results instead of returning a single page.

**--registry-url** _url_ :   Plugin registry URL.
Overrides `TW_REGISTRY_URL` and `registry.json`.

Shows: Id, Author, Name, Version, Architecture, Verified, PublishDate.
With `--verbose`: Description, License, RequiredTwVersion.

**Examples:**

```
# Search for sensor plugins
tw plugin search --filter "name~sensor"

# Search for verified Linux ARM64 packages
tw plugin search --filter "verified==true,architecture==Linux_arm64"

# List all packages sorted by newest first
tw plugin search --all --sort "-publishDate"

# Machine-readable output for scripting
tw plugin search --filter "name~driver" --non-interactive
```

---

### tw plugin uninstall

Uninstall one or more versions of a plugin.

```
tw plugin uninstall <name[@version]> [--all]
```

**\<name\>** :   Plugin name (e.g., `my-runlet-json`) or name with explicit version (e.g., `my-runlet-json@1.0.0`).

**--all** :   Remove ALL installed versions of the plugin.
Cannot be combined with an explicit `@version`.

When no version is specified and `--all` is not used, only the **latest** installed version is removed.

**Examples:**

```
# Remove the latest version
tw plugin uninstall my-runlet-json

# Remove a specific version
tw plugin uninstall my-runlet-json@1.0.0

# Remove all versions
tw plugin uninstall my-runlet-json --all
```

---

### tw plugin list

List all discovered plugins from all source directories.

```
tw plugin list [-v] [-f <format>]
```

**-v, --verbose** :   Show additional metadata from `package.tw` (author, company, description, license, websites, etc.) in table format.

**-f, --format** _format_ :   Output format: `table` (default), `list`, or `jsonl`.
The `list` and `jsonl` formats always include all known `package.tw` fields.

**Examples:**

```
# Compact table
tw plugin list

# Detailed list with all metadata
tw plugin list --format list

# Machine-readable JSON
tw plugin list --non-interactive
```

---

### tw plugin update

Check for and apply plugin updates from the registry or GitHub.

```
tw plugin update [<name>] [--list] [--all] [--force] [--allow-unsigned]
                 [--registry-url <url>] [--registry-key <file>]
                 [--github-repo <owner/repo>]
```

**\<name\>** :   Plugin name to update (e.g. `my-plugin` or `handle/my-plugin`).
Omit when using `--all` or `--list`.

**-l, --list** :   Show available updates without applying them.

**--all** :   Update all plugins that have a known source (registry or GitHub).

**--force** :   Overwrite if the same version is already installed.

**--allow-unsigned** :   Allow packages without signatures.

**--registry-url** _url_ :   Override the per-plugin stored registry URL.

**--registry-key** _file_ :   Path to the registry's public key file.

**--github-repo** _owner/repo_ :   Override the per-plugin stored GitHub repository.

Only plugins installed from a known source (registry or GitHub) are checked.
The command reads the `.plugin-source.json` sidecar file stored during install to determine which source to query.

**Examples:**

```
# List all available updates
tw plugin update --list

# Update a specific plugin
tw plugin update arepetti/sensor-driver

# Update all plugins
tw plugin update --all

# Dry-run in CI
tw plugin update --list --non-interactive
```

---

### tw plugin info

Show detailed information about a registry plugin.

```
tw plugin info <handle/name[@version]> [--registry-url <url>] [--registry-key <file>]
```

**\<handle/name\>** :   Registry package name in the format `handle/plugin-name` or `handle/plugin-name@version`.

**--registry-url** _url_ :   Plugin registry URL (overrides `TW_REGISTRY_URL` and config).

**--registry-key** _file_ :   Path to the registry's public key file.

Displays a summary header (name, author, description, license) followed by a table of available versions/architectures with size and publish date.

**Examples:**

```
# Show all versions of a plugin
tw plugin info arepetti/sensor-driver

# Show a specific version
tw plugin info arepetti/sensor-driver@2.0.0

# Machine-readable output
tw plugin info arepetti/sensor-driver --format jsonl
```

---

## EXTENDING THE CLI

Third-party command extensions can be installed by placing a DLL next to `tw.exe`.
Extension DLLs must follow the naming convention:

```
Tinkwell.Cli.Commands.{Domain}[.{Platform}].dll
```

- **Domain** (required) — the feature area (e.g. `Mqtt`, `Coap`, `Lwm2m`).
- **Platform** (optional) — `Windows`, `Linux`, or `MacOS`.
  When present, the DLL is loaded only on the matching OS.

See the [Tinkwell.Cli.Sdk README](https://github.com/arepetti/Tinkwell/blob/main/src/app/libs/Tinkwell.Cli.Sdk/README.md) for details on building command extensions.

---

## SCRIPTING

### Output formats

Use `--format jsonl` (or `--non-interactive`) for machine-readable output.
Each line is a self-contained JSON object:

```
tw measures list --format jsonl
{"name":"voltage","value":230.5,"unit":"Volt","quantity":"ElectricPotential"}
{"name":"current","value":1.2,"unit":"Ampere","quantity":"ElectricCurrent"}
```

### Batch scripts

Create a text file with one command per line (without the `tw` prefix):

```
# setup.tw.sh
measures register temperature --quantity Temperature --unit DegreeCelsius
measures register humidity --quantity RelativeHumidity --unit Percent
measures set temperature 22.5
measures set humidity 45
events publish system-ready --verb Started --source cli
```

Run it:

```
tw run setup.tw.sh --echo
```

### Exit codes

All commands exit `0` on success and non-zero on failure.
`tw run` aborts on the first non-zero exit code.

---

## ENVIRONMENT

`tw` locates the coordinator executable in the same directory as itself.
Named pipes use the OS pipe namespace (`\\.\pipe\` on Windows, `/tmp/` on Unix).

**Wizard pack discovery** uses:

1. App-local directory: `{app-directory}/packs/init/`
2. Environment variable: `TINKWELL_INIT_PACK_PATH`
3. Command-line option: `--pack-path`

**Plugin source configuration** is resolved from (highest priority first):

1. Command-line option (`--registry-url`, `--registry-key`, `--github-repo`)
2. Environment variable (`TW_REGISTRY_URL`, `TW_REGISTRY_PUBLIC_KEY_FILE`, `TW_GITHUB_REPO`)
3. Configuration file at `{LocalApplicationData}/Tinkwell/registry.json`

The configuration file format:

```json
{
  "url": "https://plugins.tinkwell.io",
  "publicKeyFile": "C:\\keys\\registry.pub",
  "githubRepo": "arepetti/tinkwell-static-plugins-registry"
}
```

When installing by package name (`handle/plugin-name`), the registry is tried first (if a URL is configured).
If the registry is not configured or the package is not found there, GitHub Releases is used as a fallback.
The GitHub repository defaults to `arepetti/tinkwell-static-plugins-registry`.

**GitHub Release conventions:** each plugin version is published as a GitHub Release tagged `{plugin-name}@{version}`.
The `.zip` asset is named `{plugin-name}-{version}.zip` for platform-agnostic plugins (the common case for C# AnyCPU assemblies), or `{plugin-name}-{version}-{architecture}.zip` for the rare platform-specific plugin.

---

## TUTORIAL: Creating and Installing a Plugin

This walkthrough shows how to take a directory of DLLs, package them as a signed Tinkwell plugin, and install it.
There are two approaches: a **quick single-step** workflow using `--from-content`, and the **traditional multi-step** workflow that gives you full control over the package structure.

### 1. Prepare the plugin files

Assume you have built your plugin with `dotnet publish`:

```
my-runlet-json/
├── My.Runlet.Json.dll
├── My.Runlet.Json.deps.json
└── Newtonsoft.Json.dll
```

### 2. Generate a signing key pair

```bash
tw identity generate-key publisher.key publisher.pub
```

Keep `publisher.key` secret.
Distribute `publisher.pub` to anyone who needs to verify your packages.

### 3a. Quick workflow (--from-content)

Pack the directory directly -- no intermediate folder structure needed:

```bash
# With an existing manifest file
tw package pack my-runlet-json/ my-runlet-json-1.0.0.zip \
  --key publisher.key --from-content --manifest my-runlet-json.tw

# Or with interactive prompts (will ask for name, version, author, etc.)
tw package pack my-runlet-json/ my-runlet-json-1.0.0.zip \
  --key publisher.key --from-content
```

You can prepare the manifest once with `tw package create-manifest` and reuse it across builds:

```bash
tw package create-manifest my-runlet-json.tw \
  --set name=my-runlet-json --set version=1.0.0 \
  --set author="Jane Doe" --set license=MIT \
  --set description="JSON transform runlet for sensor data" \
  --set "product-version=[0.1,)"
```

Skip to **step 4** below.

### 3b. Traditional multi-step workflow

Create the standard package directory structure manually:

```bash
mkdir my-runlet-json-pkg
mkdir my-runlet-json-pkg/content
```

Copy your plugin files into `content/`:

```bash
cp my-runlet-json/* my-runlet-json-pkg/content/
```

Create the manifest using `tw package create-manifest`:

```bash
tw package create-manifest my-runlet-json-pkg \
  --set name=my-runlet-json \
  --set version=1.0.0 \
  --set author="Jane Doe" \
  --set description="JSON transform runlet for sensor data" \
  --set license=MIT \
  --set "product-version=[0.1,)"
```

The `product-version` property is optional but recommended -- it ensures the plugin is only installed on compatible Tinkwell versions (here: 0.1.0 or later).
The value uses [NuGet version range syntax](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#version-ranges).

You can also run `tw package create-manifest my-runlet-json-pkg` without `--set` to be prompted interactively for each field.

Your directory should now look like:

```
my-runlet-json-pkg/
├── package.tw
└── content/
    ├── My.Runlet.Json.dll
    ├── My.Runlet.Json.deps.json
    └── Newtonsoft.Json.dll
```

Pack and sign:

```bash
tw package pack my-runlet-json-pkg my-runlet-json-1.0.0.zip --key publisher.key
```

### 4. Verify (optional)

```bash
tw package verify my-runlet-json-1.0.0.zip --key publisher.pub
```

### 5. Install the plugin

```bash
tw plugin install my-runlet-json-1.0.0.zip --key publisher.pub
```

The plugin is now available at `{LocalApplicationData}/Tinkwell/plugins/my-runlet-json@1.0.0/` and can be referenced in configuration:

```
runlet json from "My.Runlet.Json.dll"
```

### 6. Update to a new version

When you release version 1.1.0, use `--update` to install and clean up the old version in one step:

```bash
tw plugin install my-runlet-json-1.1.0.zip --key publisher.pub --update
```

### 7. List and remove

```bash
# See all installed plugins
tw plugin list

# Remove a specific version
tw plugin uninstall my-runlet-json@1.0.0

# Or remove everything
tw plugin uninstall my-runlet-json --all
```

---

## SEE ALSO

[Configuration Guide](configuration.md), [Expressions Reference](expressions.md), [How-To Guide](how-to.md), [Units Reference](units.md), [Plugins Reference](../reference/plugins.md), Plugin Registry CLI, Plugin Registry Admin CLI, Plugin Registry API (see the [tinkwell-plugins](https://github.com/arepetti/tinkwell-plugins-repository) repository)
