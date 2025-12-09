# Supervisor

The Supervisor is responsible for reading the ensamble configuration and spawning the required child processes (runners).

## Generic Loading Sequence

```mermaid
sequenceDiagram
    autonumber

    participant Supervisor
    participant App Configuration
    participant Ensamble Parser
    participant Process Builder
    participant Child Process

    Supervisor->>App Configuration: Read Ensamble file path
    App Configuration-->>Supervisor: Return file path
    Supervisor->>Ensamble Parser: Parse configuration file
    Ensamble Parser->>Ensamble Parser: Load, parse, resolve imports, and apply conditionals
    Ensamble Parser-->>Supervisor: Return list of Runners
    loop For each Runner
        Supervisor->>Process Builder: Create child process for Runner
        Process Builder-->>Child Process: Start process
        Child Process-->>Supervisor: Send "ready" signal
    end
    Note over Supervisor: Initialization complete
```

## Service Address Resolution

```mermaid
sequenceDiagram
    autonumber

    participant User Code
    participant ServiceLocator
    participant HostingInformation
    participant Supervisor
    participant Discovery Service

    User Code->>ServiceLocator: Find*("service name")
    ServiceLocator->>HostingInformation: ResolveDiscoveryServiceAddressAsync()
    alt Env var TINKWELL_DISCOVERY_SERVICE_ADDRESS is set
        HostingInformation-->>ServiceLocator: Return address from env var
    else Env var is not set
        HostingInformation->>Supervisor: "roles query discovery"
        Supervisor-->>HostingInformation: Return discovery service address
        HostingInformation-->>ServiceLocator: Return address
    end
    ServiceLocator->>Discovery Service: gRPC call to resolve service address
    Discovery Service-->>ServiceLocator: Return service address
    ServiceLocator->>ServiceLocator: Create gRPC channel and client
    ServiceLocator-->>User Code: Return gRPC client
```