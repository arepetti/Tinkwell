# Tinkwell.Events.Abstractions

Contract types for the Tinkwell event subsystem: `IEventPublisher`, `EventEnvelope`, and `EventVerb`.
Depend on this package when you need to publish or consume events without pulling in the gRPC transport implementation.

For the full event publishing implementation (resilient gRPC transport, retry logic), see **Tinkwell.Events**.

See [docs/reference/events.md](../../docs/reference/events.md) for the event model and configuration reference.
