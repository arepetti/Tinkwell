# Named-pipe security

This document describes the threat model for the named-pipe inter-process communication channel used between the Tinkwell coordinator and its runners.

## Overview

Tinkwell uses **line-oriented JSONL** over Windows named pipes (via `NamedPipeServerStream` / `NamedPipeClientStream`).

- **Coordinator command pipe** (`PipeServer` + `PipeConnection` in `Tinkwell.Pipes`, hosted by the coordinator) receives one command per connection.
  The `PipeCommandDispatcher` reads a single line, dispatches Spectre CLI commands (for example `config read`, `endpoint allocate`, `service register`, `notify ready`, `quit`), and writes one response line.
  The **same pipe name** is used by: the `tw` CLI (`PipeCommandRunner`), and each runner’s `CoordinatorPipeClient` (connects per operation with `PipeClient`, default **10s** connect+read timeout).

- **Sentinel pipe** (`SentinelPipeServer` in the coordinator) is a long-lived second pipe whose name is derived as `{commandPipeName}-sentinel`.
  Runners open it from `SentinelPipeClient` and keep the connection open so that if the coordinator exits, the OS tears down the pipe and runners can shut down.
  Shutdown may send a `quit` line on those connections.

There is a separate **gRPC/HTTP/2** data plane (Kestrel on runners); this document is only about **named pipes** for control plane traffic.

## Trust boundary

The pipes are **local to the machine** and do not traverse the network.
In typical operation the coordinator and all runners are child processes of the same user session, started by the same coordinator, with pipe names passed on the runner command line (`--coordinator-pipe`, `--sentinel-pipe`).

- **Default pipe name** is `tinkwell-coordinator` (configurable under `Coordinator:PipeServer:PipeName`); if the base name is in use and `AllowPipeNameFallback` is true, the server may bind to `tinkwell-coordinator-1`, `tinkwell-coordinator-2`, etc., and log the **resolved** name.
  The name is **not** a secret token: it is a well-known or operator-chosen string, not a high-entropy random identifier.

- The implementation does **not** enable client **impersonation** on the server: `NamedPipeServerStream` is constructed with the standard API only (no custom `PipeOptions` for impersonation).
  OS default named-pipe access rules still apply: on Windows, other users’ processes generally cannot connect to a pipe created by a different user under default security.

- **No encryption or message signing** is applied in application code: payload integrity and confidentiality depend on the OS isolating the pipe to appropriate principals.

## Threats

### 1. Local privilege escalation

A higher-privileged local process that can influence or attach to the coordinator’s pipe could affect control-plane behavior.
Tinkwell does not use named-pipe **impersonation** for clients, and the server does not expose `PipeAccessRights` for impersonation in the constructors used here, which avoids a common named-pipe pivot into the server’s security context.
Pipe **names** are not designed as a cryptographic barrier; the primary line of defense is **OS identity** (which user/session created the pipe) and **operational** control of who can run code that knows the name and is allowed to open the client.

### 2. Unauthorized local access

Any process with sufficient rights to open `NamedPipeClientStream` to the same `PipeName` (and machine id `.` in runner code) could send a line and receive a response, **if** it can name the correct pipe.
That includes other processes in the same security context (same user) that learn or guess the name.
On Windows, another **user** session is typically **not** able to connect by default.
There is no application-level access control list beyond what the OS provides; custom `PipeSecurity` is not set in the current code.

### 3. Denial of service

The accept loop in `PipeServer` spawns a handler per connection; a local attacker could open many connections and tie up the coordinator (CPU, memory for per-connection work).
The server allows up to `NamedPipeServerStream.MaxAllowedServerInstances` server instances, and each handler can be limited by `PipeServerOptions.ConnectionTimeoutMs` (**default 30,000** ms) while waiting for the client command line—after that, the server cancels the handler.
`PipeClient` and `PipeCommandRunner` use timeouts (**10s**) on the full connect/write/read path.

There is **no** hard cap on a **single line’s length** in `PipeConnection` (line reads are unbounded beyond available memory); extremely large lines could stress memory.
**No** per-PID or per-user rate limits are implemented in Tinkwell.

### 4. Message tampering / replay

Messages are **plain text** JSONL over the pipe, not signed or encrypted.
A process that can write to the pipe can inject a command line; replay of a past line is possible if a peer can resend it.
This is **accepted** in the current **same-user, local-machine** trust model.
If the trust boundary ever **expands** to multiple users, machines, or untrusted co-tenants, use stronger channel security (for example **TLS** over a vetted transport, or **gRPC** with mutual TLS and policy) instead of expecting secrecy from named pipes alone.

## Recommendations for future hardening

- Add message authentication (HMAC) if the trust boundary expands.
- Consider switching to Unix domain sockets on Linux for finer-grained access control (if non-Windows paths become first-class), or explicit `PipeSecurity` / Windows ACLs where needed.
- Rate-limit or cap concurrent pipe connections, and add a **maximum command line / line length** to mitigate memory exhaustion.
- Consider **rate-limiting** incoming connections per source PID (not present today).

## See also

- [Coordinator–runner model](coordinator-runner.md) — IPC overview including command and sentinel pipes
- [TLS configuration](../reference/https.md) — runner HTTPS for gRPC (not the named pipes)
