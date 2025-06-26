# Supervisor

You need to have a valid certificate, both during development and when deploying to production, see [Setup.md](Setup.md).

The supervisor is in charge of starting and monitoring all the runners configured in the ensamble file. By default it reads `system.ensamble` but you can change it using the `Ensamble:Path` in `appconfig.json`.

See [ensamble syntax](Ensamble.md) to understand how to create your own ensamble files.

## Startup process

TODO

### Orchestration

`Tinkwell.Orchestrator.dll` adds a gRPC service `Orchestrator` you can use to monitor your runners. It's not included by default then you have to explicitely add it in `system.ensamble` if needed.

```
runner grpchost "Tinkwell.Bootstrapper.GrpcHost" {
	service runner orchestrator "Tinkwell.Orchestrator.dll" {}
}
```

The supervisor also exposes a named pipe to send simple text commands: it's used internally by the other hosts to communicate with the Orchestrator during the startup phase (for example to obtain the address/port Kestrel should listen to) and you won't need to know about it unless you're writing your own version of one of the hosts or if you're writing your own non-hosted runner.

For reference, these are the supported commands:

* `exit` terminate the named pipe connection.
* `signal` is sent by the runner when it's been fully initialized and it's ready.
* `runners list` obtain a comma-separated list of all the runners in the system.
* `runners start <[name]|[--pid <pid>]>` start the runner with the specified name.
* `runners stop <[name]|[--pid <pid>]>` stop the runner with the specified name (kills the process).
* `runners restart <[name]|[--pid <pid>]>` restart the runner with the specified name.
* `runners add <name> <path>` starts the specified process and register it as runner.
* `runners get <[name]|[--pid <pid>]>` obtain the host (for example `https://localhost:5000`) for the runner with the specified name or process ID.
* `endpoints claim <machine name> <runner name>` obtain the port number to use for Kestrel for the runner with name `runner name` running on the machine `machine name`.
* `endpoints query <runner name>` obtain the host (for example `https://localhost:5000`) assigned to the runner with the specified name.
* `roles claim <role name> <machine name> <runner name>` claim for runner with name `runner name`, running on the machine with name `machine name`, the role `role name` (for example `discovery`). Return the name of the runner currently in the role (it's not the same as `runner name` when someone else claimed the role before). It's used to coordinate multiple runners possibly exposing the same service (like `Tinkwell.Bootstrapper.GrpcHost`) before the `Discovery` service is active, you probably won't ever need to use this command.