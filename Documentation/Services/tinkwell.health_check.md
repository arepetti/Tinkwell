# HealthCheck Service

## Methods

### Check(HealthCheckRequest) returns (HealthCheckResponse)

## Messages

### HealthCheckRequest

(No fields)

### HealthCheckResponse

| Field | Type | Description |
|---|---|---|
| `name` | `string` | |
| `status` | `ServingStatus` | |
| `message` | `optional string` | |

## Enums

### ServingStatus (nested in HealthCheckResponse)

| Name | Value |
|---|---|
| `UNKNOWN` | 0 |
| `SERVING` | 1 |
| `NOT_SERVING` | 2 |
| `DEGRADED` | 3 |
