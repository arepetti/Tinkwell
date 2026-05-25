# Tinkwell.Runlet.Store.Replication

> **Experimental** — this runlet is a work-in-progress.
> Configuration options and behavior are subject to change.

Companion gRPC runlet that adds master-slave replication to the state store.
A master streams its full state and subsequent changes to a connected slave so that a second Tinkwell instance keeps an eventually-consistent copy of the store.

## How it works

The replication runlet runs **alongside** the `store` runlet in the same gRPC runner (shared DI).
It must be listed **after** the store runlet in the `.tw` file.

- **Master** — exposes a `StoreReplication.Replicate` gRPC endpoint.
  When a slave connects it receives a full snapshot of the store followed by a continuous stream of live changes.
- **Slave** — connects to the master, applies the snapshot, then applies live changes to its local backend.
  Until the first snapshot completes, the store returns `UNAVAILABLE` for all RPCs.
  After that, reads work normally but writes are rejected with `FAILED_PRECONDITION` (the slave is read-only).

If the connection drops, the slave reconnects with exponential backoff and performs a new full snapshot.
Reads continue to be served from the previous sync while reconnecting.

## Configuration

### Master

```tw
runner store-runner from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "db"
        load-initial-state = "yes"
    }

    runlet store-replication from "Tinkwell.Runlet.Store.Replication.dll" {
        role = "master"
    }
}
```

### Slave (on a separate machine)

```tw
runner store-runner from "Tinkwell.Runner.Grpc.dll" {
    runlet store from "Tinkwell.Runlet.Store.dll" {
        storage = "memory"
    }

    runlet store-replication from "Tinkwell.Runlet.Store.Replication.dll" {
        role = "slave"
        master-address = "192.168.1.10:4901"
    }
}
```

### Settings

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `role` | yes | — | `master` or `slave`. |
| `master-address` | slave only | — | `host:port` of the master's gRPC endpoint. |
| `reconnect-seconds` | no | `5` | Max backoff (seconds) between reconnection attempts. |

## Key types

- `StoreReplicationRunlet` — `IGrpcRunlet` entry point.
- `StoreReplicationService` — master-side gRPC service (snapshot + live stream).
- `ReplicationWorker` — slave-side `BackgroundService` (connect, apply, reconnect).

## Dependencies

- **`Tinkwell.Runlet.Store`** — accesses `IStoreBackend` and `StoreNotifier` via shared DI (`InternalsVisibleTo`).
