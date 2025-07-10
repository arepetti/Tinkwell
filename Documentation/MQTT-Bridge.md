# MQTT Bridge

The MQTT Bridge is an [agent](./Glossary.md#agent) that connects to an MQTT broker, subscribes to topics, and translates incoming messages into Tinkwell measures. This allows for seamless integration of IoT devices and other systems that use MQTT into the Tinkwell ecosystem.

## Ensamble Configuration

To use the MQTT Bridge, you need to compose it as an agent in your `ensamble.tw` file. The agent is packaged in `Tinkwell.Bridge.MqttClient.dll`.

```tinkwell
// file: ensamble.tw

compose agent mqtt_client "Tinkwell.Bridge.MqttClient.dll" {
    broker_address: "mqtt.example.com"
}
```

### Properties

The agent's behavior is configured through properties within the `compose` block.

| Attribute | Type | Required | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `broker_address` | String | No | `localhost` | The address of the MQTT broker. |
| `broker_port` | Integer | No | `1883` | The port of the MQTT broker. |
| `topic_filter` | String | No | `sensor/+` | The MQTT topic filter to subscribe to. Uses standard MQTT wildcard syntax. |
| `client_id` | String | No | `TinkwellMqttClient` | The client ID to use when connecting to the broker. |
| `username` | String | No | `null` | The username for authentication. Supports environment variables expansion. |
| `password` | String | No | `null` | The password for authentication. Supports environment variables expansion. |
| `mapping` | String | No | `null` | The path to the optional mapping file. If not provided, a default mapping is used. |

## Mapping File

The mapping file defines how incoming MQTT messages are translated into Tinkwell measures. It's a simple text file where each line represents a parsing rule. If no mapping file is provided via the `mapping` property, a default behavior is applied. Lines starting with `//` or `#` are treated as comments and ignored.

Note that **all the rules matching the topic are executed** and produce a value for a measure!

### Default Behavior

If no `mapping` file is specified, the bridge uses a default rule that is suitable for simple sensors publishing numeric values.
*   **Topic Match:** It matches any topic (`*`).
*   **Measure Name:** It takes the last segment of the topic path. For example, for a topic `sensors/living_room/temperature`, the measure name will be `temperature`.
*   **Measure Value:** It parses the entire message payload as a floating-point number.

This default is useful for simple sensors that publish a numeric value to a unique topic per reading, like `sensor/temperature` with a payload of `21.5`.

### Rule Syntax

Each rule in the mapping file follows this structure:

`<topic_pattern>=<name_definition>[:<value_definition>]`

-   `<topic_pattern>`: A wildcard pattern that is matched against the incoming MQTT topic. It uses Git-like wildcards (`*` for any sequence, `?` for a single character).
-   `<name_definition>`: Defines the name of the Tinkwell measure. This can be a static name or a dynamic [expression](./Expressions.md).
-   `<value_definition>`: An optional [expression](./Expressions.md) that is evaluated to extract the value from the message.

The expressions has access to two variables:
*   `topic`: The full topic of the MQTT message (e.g., `"sensor/temperature"`).
*   `payload`: The payload of the MQTT message as a string (e.g., `"{ "value": 21.5 }"`)

#### Name Definition

The measure name can be defined in two ways:

*  **Static Name:** A simple, unquoted string.
    ```
    sensor/temperature=living_room_temp
    ```
*  **Dynamic Expression:** A quoted string that is evaluated as an [expression](./Expressions.md). This is useful for extracting the name from the topic or payload. The expression has access to `topic` and `payload` variables.
    ```
    sensor/temperature/data="concat('living_room_', segment_at(topic, '/', 1))"
    ```

#### Value Definition

The value is always defined by an [expression](./Expressions.md) that is evaluated at runtime. 

The expression can use any of the functions available in Tinkwell expressions, which is powerful for parsing complex payloads like JSON.

### Complete Example

Consider an MQTT broker where devices publish JSON data to topics like `devices/thermostat-01/data`.

**MQTT Message:**
-   **Topic:** `devices/thermostat-01/data`
-   **Payload:** `{"temp": 22.4, "humidity": 45.8, "battery": 98.1}`

**Mapping File (`mqtt_mapping.ini`):**

```
// Note that we have two matches because we want to produce two measures from
// the same payload!
devices/thermostat-01/data=temperature:"json_get_value(payload, 'temp')"
devices/thermostat-01/data=humidity:"json_get_value(payload, 'humidity')"
```

**Resulting Measures:**

Based on the example message and mapping file, the bridge would update the following measures in the Tinkwell Store:

-   `temperature`: `22.4`
-   `humidity`: `45.8`
