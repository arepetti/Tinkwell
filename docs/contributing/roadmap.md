# Roadmap

Planned features, improvements, and exploration ideas.

## Features

- **Boolean measures** — Measures currently store numeric values only.
  Support for boolean measures (e.g. digital inputs, relay states, presence detection) would allow `true`/`false` values with proper signal evaluation and display.

- **DTLS** — (CoAP) Transport-layer security.
  Deferred to same timeline as MQTT TLS/mTLS.

- **LwM2M remaining work** — The `lwm2m` runlet implements the first 80%.
  See [LwM2M reference](../reference/lwm2m.md) for details.
  Remaining:
  - **Bootstrap** (object /0, `/bs` endpoint) — most deployments use factory bootstrap.
  - **Execute** operation — requires per-resource handler callbacks.
  - **Firmware Update** (object /5) — complex state machine, out of scope.
  - **Access Control** (object /2) — no auth model in Tinkwell yet.
  - **DTLS** — required for production; depends on DTLS feature above.
  - **SenML-CBOR** encoding — deferred (needs external library).
  - **Composite read/write** — multiple resources in one request.

## Code quality

- **Hot reload / runtime reconfiguration** — The `.tw` config is loaded at startup and changes require a full restart.
  Start with file-watch + reload for the parts of the config that don't affect the process topology (measures, signals, actions).

## Ideas / exploration

- **AI agent plugin** — A plugin runlet (`Tinkwell.Runlet.AiAgent`) that connects to an LLM and exposes Tinkwell services as MCP tools, enabling natural-language interaction with the system.
  Two operating modes:
  - **Conversational**: responds to user prompts arriving via input channels (MQTT topic, CoAP endpoint).
  - **Autonomous monitor**: reviews system state via event bus subscriptions and measure watches, reasons about trends and anomalies, and proactively publishes observations or corrective actions.

  Security topics requiring further investigation:
  - Caller identity and authorization.
  - Action filtering and guardrails.
  - Rate limiting and cost controls for cloud API calls.
  - Audit trail — log all AI decisions and tool calls to the event bus.
  - Scope restriction — which MCP tools are available to which input channel.
  - Autonomous mode safeguards — preventing runaway actuation loops.
