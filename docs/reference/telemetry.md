# Telemetry catalog

All OpenTelemetry metrics, counters, and traces collected by the Tinkwell platform.
Telemetry is registered via `AddTinkwellTelemetry()` and exported over OTLP when `Telemetry:OtlpEndpoint` is set in configuration.

---

## Assemblies

| Assembly                        | Meter                    | Activity source          |
| ------------------------------- | ------------------------ | ------------------------ |
| `Tinkwell.Coordinator`          | `Tinkwell.Coordinator`   | `Tinkwell.Coordinator`   |
| `Tinkwell.Runner.Hosting`       | `Tinkwell.Runner`        | `Tinkwell.Runner`        |
| `Tinkwell.Expressions`          | `Tinkwell.Expressions`   | `Tinkwell.Expressions`   |
| `Tinkwell.Configuration.Parser` | `Tinkwell.Configuration` | `Tinkwell.Configuration` |
| `Tinkwell.Runlet.Mqtt`          | `Tinkwell.Mqtt`          | `Tinkwell.Mqtt`          |
| `Tinkwell.Runlet.Coap`          | `Tinkwell.Coap`          | `Tinkwell.Coap`          |

---

## Counters

| Instrument name                          | Meter                    | Description                              | Tags                                                             |
| ---------------------------------------- | ------------------------ | ---------------------------------------- | ---------------------------------------------------------------- |
| `tinkwell.coordinator.runners_launched`  | `Tinkwell.Coordinator`   | Runner processes launched                |                                                                  |
| `tinkwell.coordinator.runners_crashed`   | `Tinkwell.Coordinator`   | Runner process crashes                   |                                                                  |
| `tinkwell.coordinator.runners_restarted` | `Tinkwell.Coordinator`   | Runner process restarts                  |                                                                  |
| `tinkwell.coordinator.commands`          | `Tinkwell.Coordinator`   | Pipe commands dispatched                 |                                                                  |
| `tinkwell.runner.runlets_loaded`         | `Tinkwell.Runner`        | Runlets loaded                           |                                                                  |
| `tinkwell.runner.discovery_calls`        | `Tinkwell.Runner`        | Service discovery attempts               | `service.name`, `discovery.result` (`found`/`not_found`/`error`) |
| `tinkwell.runner.channel_cache_hits`     | `Tinkwell.Runner`        | Reused an existing gRPC channel          | `channel.host`                                                   |
| `tinkwell.runner.channel_cache_misses`   | `Tinkwell.Runner`        | Created a new gRPC channel (cache miss)  | `channel.host`                                                   |
| `tinkwell.expressions.evaluations`       | `Tinkwell.Expressions`   | Expression evaluations                   |                                                                  |
| `tinkwell.expressions.timeouts`          | `Tinkwell.Expressions`   | Expression evaluation timeouts           |                                                                  |
| `tinkwell.config.files_parsed`           | `Tinkwell.Configuration` | Configuration files parsed               |                                                                  |
| `tinkwell.config.includes_resolved`      | `Tinkwell.Configuration` | Include directives resolved              |                                                                  |
| `tinkwell.mqtt.connect_attempts`         | `Tinkwell.Mqtt`          | MQTT broker connection attempts          | `mqtt.connection`, `connect.result` (`success`/`error`)          |
| `tinkwell.coap.requests`                | `Tinkwell.Coap`          | Total CoAP requests received             | `coap.server`, `coap.method`, `coap.path`                        |

---

## Histograms

| Instrument name                                | Meter                    | Unit | Description                                         | Tags                             |
| ---------------------------------------------- | ------------------------ | ---- | --------------------------------------------------- | -------------------------------- |
| `tinkwell.coordinator.runner_startup_duration`  | `Tinkwell.Coordinator`   | ms   | Time from runner launch to ready signal             |                                  |
| `tinkwell.coordinator.command_duration`          | `Tinkwell.Coordinator`   | ms   | Pipe command processing duration                    |                                  |
| `tinkwell.runner.host_build_duration`           | `Tinkwell.Runner`        | ms   | Host building duration in a runner                  |                                  |
| `tinkwell.runner.startup_duration`              | `Tinkwell.Runner`        | ms   | Total runner startup time (parse to ready)          |                                  |
| `tinkwell.runner.discovery_duration`            | `Tinkwell.Runner`        | ms   | Service discovery via the coordinator pipe          | `service.name`                   |
| `tinkwell.runner.channel_create_duration`       | `Tinkwell.Runner`        | ms   | gRPC channel creation (fresh or pooled)             | `channel.host`, `channel.cached` |
| `tinkwell.expressions.duration`                 | `Tinkwell.Expressions`   | ms   | Expression evaluation duration                      |                                  |
| `tinkwell.config.parse_duration`                | `Tinkwell.Configuration` | ms   | Configuration file parsing duration                 |                                  |
| `tinkwell.mqtt.connect_duration`                | `Tinkwell.Mqtt`          | ms   | MQTT broker connection establishment                | `mqtt.connection`                |
| `tinkwell.coap.request_duration`               | `Tinkwell.Coap`          | ms   | CoAP request processing duration                    | `coap.server`                    |

---

## Traces

| Activity name                  | Source                   | Description                                    | Tags                                      |
| ------------------------------ | ------------------------ | ---------------------------------------------- | ----------------------------------------- |
| `coordinator.start`            | `Tinkwell.Coordinator`   | Coordinator startup                            |                                           |
| `coordinator.runner.launch`    | `Tinkwell.Coordinator`   | Full runner launch sequence                    | `runner.name`, `runner.id`                |
| `coordinator.runner.wait_ready`| `Tinkwell.Coordinator`   | Wait for a runner to signal ready              | `runner.name`                             |
| `coordinator.process.launch`   | `Tinkwell.Coordinator`   | OS process creation for a runner               | `runner.name`, `process.pid`              |
| `coordinator.runner.restart`   | `Tinkwell.Coordinator`   | Runner crash-and-restart cycle                 | `runner.name`, `runner.id`                |
| `coordinator.command.dispatch` | `Tinkwell.Coordinator`   | Pipe command dispatch to a runner              | `pipe.command`, `result`                  |
| `runner.lifecycle`             | `Tinkwell.Runner`        | Full runner lifecycle (start to stop)          | `runner.id`, `runner.name`                |
| `runner.fetch_config`          | `Tinkwell.Runner`        | Fetch configuration from coordinator           |                                           |
| `runner.initialize`            | `Tinkwell.Runner`        | Runner initialization                          |                                           |
| `runner.build_host`            | `Tinkwell.Runner`        | Build the generic host                         |                                           |
| `runner.start_host`            | `Tinkwell.Runner`        | Start the generic host                         |                                           |
| `runner.notify_ready`          | `Tinkwell.Runner`        | Notify coordinator that runner is ready        |                                           |
| `runner.load_runlets`          | `Tinkwell.Runner`        | Load all runlet assemblies                     |                                           |
| `runner.validate_runlet`       | `Tinkwell.Runner`        | Validate a single runlet                       | `runlet.name`, `runlet.assembly`          |
| `runner.start_runlets`         | `Tinkwell.Runner`        | Start all runlets                              |                                           |
| `runner.start_runlet`          | `Tinkwell.Runner`        | Start a single runlet                          | `runlet.name`                             |
| `runner.stop_runlets`          | `Tinkwell.Runner`        | Stop all runlets                               |                                           |
| `runner.stop_runlet`           | `Tinkwell.Runner`        | Stop a single runlet                           | `runlet.name`                             |
| `runner.pipe.send`             | `Tinkwell.Runner`        | Send a command over the coordinator pipe       | `pipe.command`                            |
| `runner.discovery`             | `Tinkwell.Runner`        | Service discovery round-trip                   | `service.name`, `discovery.result`        |
| `runner.channel.create`        | `Tinkwell.Runner`        | gRPC channel creation (cache hit or miss)      | `channel.host`, `channel.cached`          |
| `expressions.evaluate`         | `Tinkwell.Expressions`   | Single expression evaluation                   |                                           |
| `config.parse`                 | `Tinkwell.Configuration` | Parse a `.tw` configuration file               | `config.path`                             |
| `config.include`               | `Tinkwell.Configuration` | Resolve an `include` directive                 | `include.path`                            |
| `mqtt.connect`                 | `Tinkwell.Mqtt`          | Full connection attempt (including retries)    | `mqtt.connection`, `connect.result`       |
| `coap.request`                 | `Tinkwell.Coap`          | Full CoAP request processing                   | `coap.server`, `coap.method`, `coap.path`, `coap.response_code` |

---

## Enabling telemetry

Set the OTLP endpoint in configuration to start exporting:

```
Telemetry:OtlpEndpoint = http://localhost:4317
```

Or in `ensemble.tw` runner settings:

```
runner my-runner {
    Telemetry:OtlpEndpoint = "http://localhost:4317"
    ...
}
```

All meters and activity sources are registered automatically when a runner or the coordinator starts.
