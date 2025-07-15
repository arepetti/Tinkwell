# Tinkwell: Orchestrating Modular Systems at the Edge

In today's distributed environments, from industrial IoT to lab automation, building resilient and adaptable systems presents unique challenges. Tinkwell emerges as a lightweight, configuration-driven framework designed to simplify the orchestration of modular components, particularly at the edge.

Tinkwell offers a compelling alternative for building resilient, modular applications at the edge. Its focus on configuration-driven deployment, lightweight footprint, and robust inter-process communication makes it ideal for scenarios where traditional solutions are too heavy or inflexible.

For more insights into the vision behind Tinkwell, read [Tinkwell: Firmware-less IoT and Lab Automation](https://dev.to/adriano-repetti/tinkwell-firmware-less-iot-and-lab-automation-2gef).

## The Distributed Challenge

Traditional monolithic applications often struggle with the dynamic nature of edge deployments, where connectivity can be intermittent and resources constrained. The complexities of managing diverse processes, ensuring data integrity, and reacting to real-time events demand a different approach. For a deeper dive into these architectural pressures, explore [IoT Architectures Under Pressure](https://dev.to/adriano-repetti/iot-architectures-under-pressure-why-implementation-isnt-as-simple-as-it-seems-part-1-3inn).

## Tinkwell's Approach: Composable Modularity

At its core, Tinkwell empowers you to define your system's behavior through declarative configuration files. A central **Supervisor** orchestrates a network of independent processes, or **runners**, each dedicated to a specific task. Communication flows seamlessly via gRPC, enabling robust inter-process coordination and service discovery.

This modularity fosters resilience and adaptability, allowing you to evolve your system by simply updating configuration, without complex redeployments.

## Key Capabilities

*   **Flexible Composition**: Define your application's structure using intuitive Ensamble files (`.tw`). Easily compose services and agents, or leverage the advanced `runner` block for fine-grained control over process lifecycle and resource allocation.
*   **Data-Driven Insights**: Transform raw data into actionable intelligence. Define **measures** to track real-time data points, create **derived measures** to calculate new insights from existing data, and configure **signals** to trigger events when specific conditions are met.
*   **Event-Driven Automation**: Build reactive systems with powerful action configurations (`.twa`). Listen for system events and define automated responses, from logging to triggering external HTTP requests.
*   **Extensibility**: Integrate custom logic and external protocols with ease. Tinkwell's architecture is designed to be extended, allowing you to connect to diverse data sources and external systems.

## A Glimpse into Real-time Automation

Imagine a scenario where sensor data streams in via MQTT, needs to be stored, analyzed, and potentially trigger alerts. Tinkwell simplifies this.

First, we compose our MQTT client bridge in `ensamble.tw`:

```tinkwell
// ensamble.tw
compose service mqtt_bridge "Tinkwell.Bridge.MqttClient.dll" {
    topic_filter: "sensor/+"
}
```

Next, we define our measures and a signal in `measures.twm`. The MQTT bridge will automatically update `temperature_sensor_1` when data arrives on `sensor/temperature_sensor_1`.

```tinkwell
// measures.twm
measure temperature_sensor_1 {
    type: "Temperature"
    unit: "DegreeCelsius"

    signal high_temperature {
        when: "value > 30"
    }
}
```

Finally, we define an action in `actions.twa` to log the alert:

```tinkwell
// actions.twa
when event high_temperature {
    then {
        mqtt_send {
            topic: "home/ac/living_room/set"
            payload: "{ \"power\": \"ON\" }"
        }
    }
}
```

This simple setup demonstrates how Tinkwell seamlessly integrates data ingestion, processing, and reactive automation, all driven by clear, declarative configurations.

## Getting Started

Dive into the details and set up your first Tinkwell instance: [Getting-Started.md](./Documentation/Getting-Started.md)

## Further Exploration

*   [Glossary](./Documentation/Glossary.md): Understand core concepts.
*   [CLI Reference](./Documentation/CLI.md): Master the command-line tools.
*   [Derived Measures](./Documentation/Derived-measures.md): Learn advanced data processing.
*   [Actions](./Documentation/Actions.md): Configure event-driven automation.
*   [Ensamble](./Documentation/Ensamble.md): Deep dive into system composition.

## Testing Strategy

A robust testing strategy is crucial for ensuring the reliability and maintainability of Tinkwell. The following outlines the recommended approach for testing the `Tinkwell.Bootstrapper` and its components.

### Golden File/Snapshot Testing for Parsers

For components like the `EnsambleParser`, a **Golden File Testing** (or **Snapshot Testing**) approach is highly effective. This strategy provides a strong regression suite and serves as living documentation of the parser's capabilities.

**Methodology:**

1.  **Test Asset Directory:** Create a dedicated directory (e.g., `TestAssets/EnsambleParser/`) to store test files.
2.  **Craft Test Files:** Develop a series of `.tw` files that cover various features, from simple cases to complex scenarios involving imports, conditionals, and nested runners.
3.  **Generate Golden Files:** For each valid `.tw` file, run the parser once and serialize the resulting `RunnerDefinition` object graph to a JSON file (the "golden" or "snapshot" file). This file represents the expected output.
4.  **Automated Comparison:** Use a testing framework like xUnit with a snapshot testing library (e.g., `Verify.Xunit`) to automatically compare the parser's output against the golden files. When the parser's logic is updated, the library can help review the changes and update the golden files with a single command.

### Granular Unit Tests

To complement the high-level snapshot tests, granular unit tests should be written for individual components:

*   **`EnsambleTokenizer`:** Ensure that raw strings are correctly converted into a stream of tokens.
*   **`EnsamblePreprocessor`:** Verify that template variables are correctly replaced.
*   **`EnsambleParser` Logic:** Test the parser's logic with a pre-made list of tokens to ensure it correctly builds the object graph.

### Unit and Integration Testing for Other Components

*   **`ExpressionEvaluator` and Custom Functions:** Use `[Theory]` tests with `[InlineData]` to cover a wide range of expressions, including basic logic, parameter substitution, and error handling.
*   **`EnsambleConditionEvaluator`:** Mock `IExpressionEvaluator` and `IConfiguration` to isolate and test the filtering logic.
*   **`StrategyAssemblyLoader`:** Use integration tests with a temporary directory and dummy files to verify that the loader correctly identifies and loads assemblies based on file naming conventions and the current OS.
*   **Host Implementations (`DllHost`, `GrpcHost`):** Use unit tests with fake registrars to verify that the hosts correctly discover and invoke registrar methods.
*   **Inter-Process Communication (IPC):** Use integration tests with an in-process server to verify that the `NamedPipeClient` and `NamedPipeServer` can communicate end-to-end.
