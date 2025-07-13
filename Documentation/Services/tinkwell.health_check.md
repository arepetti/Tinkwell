# HealthCheck Service

Service implemented by all the firmlets/runners exposing their health status to the system.

## Methods

### Check(HealthCheckRequest) returns (HealthCheckResponse)
Check the health status of the runner exposing this service.

## Messages

### HealthCheckRequest
Request for HealthCheck.Check().

(No fields)

### HealthCheckResponse
Response for HealthCheck.Check(), it contains the current runner's health status.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | Name of the service to which the status refers to. |
| `status` | `ServingStatus` | Status of the service. |
| `message` | `optional string` | An optional descriptive message to explain the status (for example why performance are degraded or what caused the service to stop). |

## Enums

### ServingStatus (nested in HealthCheckResponse)
Represents the service status.

| Name | Value | Description |
|---|---|---|
| `UNKNOWN` | 0 | Unknown status. |
| `SERVING` | 1 | The service is running normally. |
| `NOT_SERVING` | 2 | The service is down, disabled or crashed and it's not performing its normal operations. |
| `DEGRADED` | 3 | The service is running and it's performing its normal operations but its performance are degraded. |