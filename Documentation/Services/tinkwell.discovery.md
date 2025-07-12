# Discovery Service

This service provides the basic methods needed to find the other registered services in the system.

## Methods

### List (DiscoveryListRequest) returns (DiscoveryListReply)
Lists all the registered services matching the specified search query.

### Find (DiscoveryFindRequest) returns (DiscoveryFindReply)
Finds the service with the specified name.

### FindAll (DiscoveryFindAllRequest) returns (DiscoveryFindAllReply)
Finds the service with the specified family name. Note that this differs from a simple Find() because it searches using only the family name and it could return multiple results (if more services with the same family name are registered). It also differs from List() because the search is not extended to any other field, only the family name is evaluated. Do not use this to pick a single service to use, use Find() instead because the Discovery Service may determine the best match trying to balance the load or using the nearest/fastest service in the pool. This is intended to be used only when you want to use them all.

### Register (DiscoveryRegisterRequest) returns (DiscoveryRegisterReply)
Registers a new service, started by a separate GRPC host. Clients do not usually need to call this method unless they started their services using a separate runner. If you try to register a service with a name which is not unique you get error 8 (already exists).

## Messages

### ServiceDescription
Describes a Service.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | Represents the unique name associated with the service. It is made of the package name and the service name, for example "Tinkwell.Discovery". In each system there can be only one service with a specified name and it is the preferred method to find as service. |
| `aliases` | `repeated string` | A list of "aliases" for this service. They are alternative names (without any specific convention) that can be used to search for the service. Each alias must be unique in the system, you should use them only if they are truly unique or if they include a namespaced package name. |
| `friendly_name` | `optional string` | An optional alternative "friendly name" for the service. It is used only for display purposes and it does not participate in the search for a service. It does not need to be unique but it should be informative. |
| `family_name` | `optional string` | An optional family name. When multiple services implements the same contract - and each one has its own unique name - you cannot easily find which service to use. The family name does not need to be unique: on the contrary, it is designed for multiple services implementing the same contract to share the same family name. Clients can then search for the contract implementation using the family name instead of a specific name. Note that only one service per host can have the same family name (but services on multiple hosts can share it). |
| `host` | `string` | The GRPC host address (same as url but without the exact API endpoint). |
| `url` | `string` | The GRPC API endpoint to call to use the service. |

### DiscoveryListRequest
Parameters for Discovery.List() call.

| Field | Type | Description |
|---|---|---|
| `query` | `optional string` | An optional filter which includes only the services containing the specified text. The match is always partial and not case sensitive. It searches name, aliases, family_name and friendly_name. |

### DiscoveryListReply
Response for Discovery.List() call.

| Field | Type | Description |
|---|---|---|
| `services` | `repeated ServiceDescription` | The list of services matching the specified filter (if present) or all the services if no filter were specified. |

### DiscoveryFindRequest
Parameters for Discovery.Find() call.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The exact name of the service to find. Searches favour exact name matches and if nothing is found then it continues searching for aliases. If nothing is found then it searches for the family name and if multiple services implement that contract then an arbitrary one is returned. Use Discovery.List() or Discovery.FindAll() if you want to implement your own logic to choose among multiple matches (but also note that this is highly discouraged because the Discovery Service can pick the best match using information not available to the caller). |

### DiscoveryFindReply
Response for Discovery.Find() call.

| Field | Type | Description |
|---|---|---|
| `host` | `optional string` | The URL for the GRPC API server hosting the service with the specified name (same as url but without the specific API endpoint). This field is not included in the response if no service matches the specified name. |
| `url` | `optional string` | The URL for the GRPC API endpoint for the service with the specified name. This field is not included in the response if no service matches the specified name. |

### DiscoveryFindAllRequest
Parameters for Discovery.FindAll() call.

| Field | Type | Description |
|---|---|---|
| `family_name` | `string` | The exact family name of the service to find. |

### DiscoveryFindAllReply
Response for Discovery.FindAll() call.

| Field | Type | Description |
|---|---|---|
| `hosts` | `repeated string` | The list of all the hosts exposing a service with the specified family name. |

### DiscoveryRegisterRequest
Parameters for Discovery.Register() call.

| Field | Type | Description |
|---|---|---|
| `service` | `ServiceDescription` | The service to register, it must be a new service with an unique name. |

### DiscoveryRegisterReply
Response for Discovery.Register() call.

(No fields)
