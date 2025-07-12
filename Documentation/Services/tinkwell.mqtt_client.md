# MqttClient Service

Service to send MQTT messages.

## Methods

### Publish (PublishMqttMessageRequest) returns (PublishMqttMessageResponse)
List all the measures currently stored.

## Messages

### PublishMqttMessageRequest
Request for MqttClient.Publish().

| Field | Type | Description |
|---|---|---|
| `topic` | `string` | The topic to publish the message to. |
| `payload` | `string` | The message to publish. |

### PublishMqttMessageResponse
Response of MqttClient.Publish().

| Field | Type | Description |
|---|---|---|
| `status` | `Status` | |

## Enums

### Status (nested in PublishMqttMessageResponse)

| Name | Value | Description |
|---|---|---|
| `OK` | 0 | The message was published successfully. |
| `NO_SUBSCRIBERS` | 1 | The message could not be published because there are no subscribers. |
| `ERROR` | 2 | The message could not be published. |
