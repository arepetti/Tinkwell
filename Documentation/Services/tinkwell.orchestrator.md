# Orchestrator Service

Optional service to manage runners.

## Methods

### List (OrchestratorListRequest) returns (OrchestratorListReply)
List all the runners currently active in the system.

### Start (StartStopRunnerRequest) returns (StartStopRunnerResponse)
Start a runner which has been stopped with a call to Stop().

### Stop (StartStopRunnerRequest) returns (StartStopRunnerResponse)
Stop an existing runner.

### Restart (StartStopRunnerRequest) returns (StartStopRunnerResponse)
Restart an existing runner (equivalent to calling Stop() followed by Start()).

### Add (AddRunnerRequest) returns (AddRunnerResponse)
Add a new runner in the target system.

## Messages

### OrchestratorListRequest
Request message for Orchestrator.list().

| Field | Type | Description |
|---|---|---|
| `query` | `optional string` | Optional query to filter the runners. If specified then only runner whose name contain the specified text are returned (comparison is case insensitive). |

### OrchestratorListReply
Response message for Orchestrator.list().

| Field | Type | Description |
|---|---|---|
| `runners` | `repeated Runner` | List of runners that satisfies the query condition in OrchestratorListRequest.query |

### Runner (nested in OrchestratorListReply)

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The full name of the runner. |

### StartStopRunnerRequest
Request messages for starting, stopping, and restarting runners.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The exact name of the runner to start/stop/restart. Name is case sensitive. |

### StartStopRunnerResponse
Response messages for starting, stopping, and restarting runners.

(No fields)

### AddRunnerRequest
Request message to add a new runner with Orchestrator.add().

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The full name of the runner to add. It must be unique in the system. |
| `path` | `string` | Path of the executable to run. It can be an absolute path, a relative path or the file name only (if it's in the working directory). On Windows the .exe extension can be omitted (so that you do not need to know the host OS when making the call). |
| `arguments` | `string` | Optional arguments to pass to the runner. It can be an empty string if no arguments are needed. |

### AddRunnerResponse
Response message for Orchestrator.add().

(No fields)
