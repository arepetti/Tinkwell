# Glossary

[A](#agent) | [C](#configuration) | [D](#derived-measure) | [E](#ensemble) | [F](#firmlet) | [G](#grpc-host) | [H](#health-check-service) | [M](#machine) | [O](#orchestrator-service) | [R](#reactor) | [S](#service) | [W](#watchdog)

---

#### Agent
A synonym for [firmlet](#firmlet), when it does not expose a gRPC service (but can consume them).

*See also:* [Firmlet](#firmlet), [gRPC host](#grpc-host), [DLL host](#dll-host)

#### Configuration
The set of files that define the system's behavior, including the [Ensemble](#ensemble) file and the configurations for [derived measures](#derived-measure).

*See also:* [Ensemble](#ensemble), [Derived Measure](#derived-measure)

#### Derived Measure
A [measure](#measure) whose value is calculated from other measures. The [Reducer](#reducer) is responsible for computing its value. Every measure is associated with its [unit of measure](./Units.md). Derived measures are defined in their own configuration file (see [Derived Measures Configuration](./Derived%20measures.md)).

*See also:* [Measure](#measure), [Reducer](#reducer), [Store (Service)](#store-service)

#### Discovery (Service)
A [service](#service) implementing the `Tinkwell.Discovery` contract, which exposes commands to search for and manage other services. There can be only one per [system](#system). The default host for services assumes this role automatically if such a service is not already registered in the system.

*See also:* [Service](#service), [Orchestrator (Service)](#orchestrator-service)

#### DLL host
A [host](#host) that loads [agents](#agent) packaged as libraries. It does not expose a web server, so it's not convenient for [services](#service).

*See also:* [Host](#host), [Agent](#agent), [Firmlet](#firmlet)

#### Ensemble
The description of a set of [runners](#runner) and their options.

*See also:* [Runner](#runner), [Firmlet](#firmlet), [Supervisor](#supervisor)

#### Event
A piece of information detailing something that happened in the system. It consists of a topic, which indicates the type of event, and a subject, verb, and object (plus an optional payload), which indicate who triggered the event and why. Subscribers can ask to receive notifications only for the topics or subjects/objects they're interested in.

*See also:* [Events Gateway](#events-gateway), [Signal](#signal), [Reactor](#reactor)

#### Events Gateway
A [service](#service) implementing the `Tinkwell.EventsGateway` contract. It accepts events published by a [firmlet](#firmlet) and broadcasts them to all its subscribers.

*See also:* [Event](#event), [Service](#service)

#### Firmlet
A generic term for a piece of logic or a [service](#service). It may coincide with a [runner](#runner) (and is often used as a synonym when it's the only piece of logic in a runner), but a single runner can contain multiple (but unrelated) firmlets.

*See also:* [Runner](#runner), [Host](#host), [Service](#service), [Agent](#agent)

#### gRPC host
A [host](#host) that loads [services](#service) packaged as libraries. It allows multiple services to share a single web server or to run in separate processes. When loaded within the same process, services can also share DI services.

*See also:* [Host](#host), [Service](#service), [Firmlet](#firmlet)

#### Health check (Service)
A [service](#service) implementing `Tinkwell.HealthCheck` with basic checks to continually monitor the state of the [host](#host) it's running in. See [Health Monitoring](./Health-monitoring.md) for details.

*See also:* [Watchdog](#watchdog), [Service](#service)

#### Host
A special [runner](#runner) without logic of its own, used to load one or more [firmlets](#firmlet). Hosts are used to reduce the boilerplate imposed on a runner by the [Supervisor](#supervisor) and to optimize for performance (for example, to host multiple gRPC services in the same web server).

*See also:* [Runner](#runner), [Firmlet](#firmlet), [gRPC host](#grpc-host), [DLL host](#dll-host)

#### Machine
The computer where an instance of Tinkwell is running. It may coincide with the [system](#system) if Tinkwell is not running on a distributed system.

*See also:* [System](#system)

#### Measure
A single point of data within the [system](#system), such as a sensor reading or a status value. It is the fundamental unit of information that the [Store (Service)](#store-service) manages.

*See also:* [Derived Measure](#derived-measure), [Store (Service)](#store-service), [Reducer](#reducer), [Reactor](#reactor)

#### Orchestrator (Service)
A [service](#service) implementing `Tinkwell.Orchestrator` that exposes commands to manage [runners](#runner) (at the [Supervisor](#supervisor) level). There can be only one per system. You can use this service to interact with the Supervisor programmatically, from the command line you can use `tw supervisor send` to send commands directly to the supervisor. 

*See also:* [Supervisor](#supervisor), [Runner](#runner), [Service](#service)

#### Reactor
An [agent](#agent) that monitors the [Store (Service)](#store-service) for changes and applies a set of rules. If a condition is met, it emits a [signal](#signal).

*See also:* [Agent](#agent), [Signal](#signal), [Store (Service)](#store-service)

#### Reducer
An [agent](#agent) that creates [derived measures](#derived-measure) from a configuration file and updates their values when one of their dependencies changes.

*See also:* [Agent](#agent), [Derived Measure](#derived-measure), [Store (Service)](#store-service)

#### Runner
A process started by the [Supervisor](#supervisor). It's often used interchangeably with _process_. It's usually (but not necessarily) an executable.

*See also:* [Supervisor](#supervisor), [Firmlet](#firmlet), [Host](#host)

#### Service
A [firmlet](#firmlet) exposing one or more gRPC services. Each service is identified and discovered using its name but services can add multiple aliases (as long as they're unique in the system) and a _family name_. A family name is important because names must be unique but sometimes you need to find all the services implementing a particular contract, that's when you use the family name: those services registers a common discoverable _family name_ and keep their _name_ unique.  

*See also:* [Firmlet](#firmlet), [gRPC host](#grpc-host), [Discovery (Service)](#discovery-service)

#### Signal
An event emitted by the [Reactor](#reactor) when one of the configured conditions is met.

*See also:* [Event](#event), [Reactor](#reactor)

#### Simple identifier
A name (for example of a [runner](#runner) or a [measure](#measure)) simple enough that does not need to be enclosed in double quotes. It must follow a few rules: it contains only alphanumeric characters (and underscore) and it cannot start with a number.

#### Store (Service)
A [service](#service) implementing `Tinkwell.Store` that keeps track of all the [measures](#measure) in the [system](#system). It's responsible for converting between different units of measure, keeping historical values, and broadcasting changes to all subscribers. There should be only one source of truth in the system but, with the appropriate configuration, you can create multiple instances to distribute the workload; however, this is not supported out-of-the-box.

*See also:* [Measure](#measure), [Derived Measure](#derived-measure), [Reducer](#reducer), [Reactor](#reactor)

#### Supervisor
The entry-point of a Tinkwell application. It's in charge of parsing the [ensemble](#ensemble) configuration file, starting the required processes, and monitoring their health at the process level. There can be only one per machine and only one master per [system](#system).

*See also:* [Runner](#runner), [Ensemble](#ensemble), [Orchestrator (Service)](#orchestrator-service)

#### System
One or more machines running a Tinkwell installation.

*See also:* [Machine](#machine)

#### Watchdog
A [firmlet](#firmlet) that periodically queries all [services](#service) implementing `Tinkwell.HealthCheck` to report on their status.

*See also:* [Health check (Service)](#health-check-service), [Firmlet](#firmlet)