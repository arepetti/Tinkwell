# MQTT Server

The MQTT Server is a lightweight MQTT broker designed for **local development** and **integration testing** within the Tinkwell ecosystem. It is implemented using the `MQTTnet.Server` library and provides a simple way to simulate an MQTT environment without requiring a full-fledged broker.

## Ensamble Configuration

To use the MQTT Server, you need to compose it as an agent in your `ensamble.tw` file. The agent is packaged in `Tinkwell.Bridge.MqttServer.dll`.

```tinkwell
// file: ensamble.tw

compose agent mqtt_server "Tinkwell.Bridge.MqttServer.dll" {
    port: 1883
}
```

### Properties

The agent's behavior is configured through properties within the `compose` block.

| Attribute | Type | Required | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `port` | Integer | No | `1883` | The port on which the MQTT broker will listen for connections. |
