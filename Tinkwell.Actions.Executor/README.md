# Who is doing what?

There are many names bouncing around here, we should clarify a little bit!

**Definition** (`WhenDefinition`, `ActionDefinition`) : represents the configuration as read from a TWA file.
We do not keep them around: we just build the objects we need and discard them.

**Listener**: represents a single listener to a specific event (or set of them, according to the filters).
It also contains the list of things to do when such an event is received. They're called _directives_.

**Directive**: represents one thing to do in abstract terms, it contains the type of the agent to use and 
the parameters to pass to it (but still the way they were defined in configuration, it means that the actual values
calculated using the payload (specific of a received event) have not been used yet.

**Intent**: this is the final object that is put into the queue of things to do, it contains the type of the agent to use
and the parameters just like a `Directive`, but also the payload of the event that triggered it.
The worker thread will then create an agent and transform the parameters into their final value using the payload.
To understand this imagine a parameter defined as `path: $"/path/to/{{ payload.name }}"`,
the worker thread will transform that template string into the final value, like `"path/to/john"` if
`payload.name` was `"john"`.

**Agent**: represents the object in charge of performing an action, using the the parameters and the payload.

## Data Flow

```mermaid
graph TD
    classDef config fill:#f0f8ff,stroke:#a4c8e0,stroke-width:1px
    classDef runtime fill:#f5fff5,stroke:#b3d9b3,stroke-width:1px
    classDef eventbus fill:#fff8f0,stroke:#e0c8a4,stroke-width:1px
    classDef data fill:#fafafa,stroke:#cccccc,stroke-width:1px

    TWA[("actions.twa<br/><i>file</i>")]:::config
    EX(Executor):::runtime
    L(Listeners):::data
    EG(EventsGateway):::eventbus
    INT(Intent):::data
    Q([Queue]):::data
    W(Worker):::runtime
    A(Agent):::runtime

    TWA -- "1 reads" --> EX
    EX -- "2 creates" --> L

    EX -- "3 subscribes" --> EG
    EG -- "4 pushes event (with payload)" ---> EX
    EX -- "5 finds matching listener" --> L
    L -- "6 provides directive" --> EX
    EX -- "7 creates intent (directive + payload)" --> INT
    INT -- "8 enqueues" --> Q
    W -- "9 dequeues" --> Q
    W -- "10 executes" --> A

    linkStyle 0 stroke:cornflowerblue,stroke-width:1.5px,color:cornflowerblue;
    linkStyle 1 stroke:cornflowerblue,stroke-width:1.5px,color:cornflowerblue;

    linkStyle 2 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 3 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 4 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 5 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 6 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 7 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 8 stroke:orange,stroke-width:1.5px,color:orange;
    linkStyle 9 stroke:orange,stroke-width:1.5px,color:orange;
```

## Enqueue/Dequeue Flow

```mermaid
graph TD
    subgraph "Listening Thread"
        direction LR
        EG(EventsGateway) -- "1 Receives event" --> L(SubscribeToEventsAsync)
    end

    subgraph "Dispatch Thread"
        direction LR
        D(DispatchIntentsAsync) -- "4 Executes action" --> A(Agent)
    end

    L -- "2 Enqueues intent" --> Q([Queue])
    Q -- "3 Dequeues intent" --> D

    linkStyle 0 stroke:cornflowerblue,stroke-width:1.5px;
    linkStyle 1 stroke:orange,stroke-width:1.5px;
    linkStyle 2 stroke:orange,stroke-width:1.5px;
    linkStyle 3 stroke:cornflowerblue,stroke-width:1.5px;
```