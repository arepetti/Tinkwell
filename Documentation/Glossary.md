# Glossary

#### Agent
A synonym for [firmlet](#firmlet), when it does not expose a gRPC service (but can consume them).

#### Configuration
The set of files that define the system's behavior, including the [Ensemble](#ensemble) file and the configurations for [derived measures](#derived-measure).

#### Derived Measure
A [measure](#measure) whose value is calculated from other measures. The [Reducer](#reducer) is responsible for computing its value. Every measure is associated with its [unit of measure](./Units.md). Derived measures are defined in their own configuration file (see [Derived Measures Configuration](./Derived%20measures.md)).

#### Discovery (Service)
A [service](#service) implementing the `Tinkwell.Discovery` contract, which exposes commands to search for and manage other services. There can be only one per [system](#system). The default host for services assumes this role automatically if such a service is not already registered in the system.

#### DLL host
A [host](#host) that loads [agents](#agent) packaged as libraries. It does not expose a web server, so it's not convenient for [services](#service).

#### Ensemble
The description of a set of [runners](#runner) and their options.

#### Event
A piece of information detailing something that happened in the system. It consists of a topic, which indicates the type of event, and a subject, verb, and object (plus an optional payload), which indicate who triggered the event and why. Subscribers can ask to receive notifications only for the topics or subjects/objects they're interested in.

#### Events Gateway
A [service](#service) implementing the `Tinkwell.EventsGateway` contract. It accepts events published by a [firmlet](#firmlet) and broadcasts them to all its subscribers.

#### Firmlet
A generic term for a piece of logic or a [service](#service). It may coincide with a [runner](#runner) (and is often used as a synonym when it's the only piece of logic in a runner), but a single runner can contain multiple (but unrelated) firmlets.

#### gRPC host
A [host](#host) that loads [services](#service) packaged as libraries. It allows multiple services to share a single web server or to run in separate processes. When loaded within the same process, services can also share DI services.

#### Health check (Service)
A [service](#service) implementing `Tinkwell.HealthCheck` with basic checks to continually monitor the state of the [host](#host) it's running in. See [Health Monitoring](./Health%20monitoring.md) for details.

#### Host
A special [runner](#runner) without logic of its own, used to load one or more [firmlets](#firmlet). Hosts are used to reduce the boilerplate imposed on a runner by the [Supervisor](#supervisor) and to optimize for performance (for example, to host multiple gRPC services in the same web server).

#### Machine
The computer where an instance of Tinkwell is running. It may coincide with the [system](#system) if Tinkwell is not running on a distributed system.

#### Measure
A single point of data within the [system](#system), such as a sensor reading or a status value. It is the fundamental unit of information that the [Store (Service)](#store-service) manages.

#### Orchestrator (Service)
A [service](#service) implementing `Tinkwell.Orchestrator` that exposes commands to manage [runners](#runner) (at the [Supervisor](#supervisor) level). There can be only one per system. You can use this service to interact with the Supervisor programmatically, from the command line (on Windows) you can use `/Development/Send-SupervisorCommand.ps1` (which communicates through a named pipe). 

#### Reactor
An [agent](#agent) that monitors the [Store (Service)](#store-service) for changes and applies a set of rules. If a condition is met, it emits a [signal](#signal).

#### Reducer
An [agent](#agent) that creates [derived measures](#derived-measure) from a configuration file and updates their values when one of their dependencies changes.

#### Runner
A process started by the [Supervisor](#supervisor). It's often used interchangeably with _process_. It's usually (but not necessarily) an executable.

#### Service
A [firmlet](#firmlet) exposing one or more gRPC services. Each service is identified and discovered using its name but services can add multiple aliases (as long as they're unique in the system) and a _family name_. A family name is important because names must be unique but sometimes you need to find all the services implementing a particular contract, that's when you use the family name: those services registers a common discoverable _family name_ and keep their _name_ unique.  

#### Signal
An event emitted by the [Reactor](#reactor) when one of the configured conditions is met.

#### Simple identifier
A name (for example of a [runner](#runner) or a [measure](#measure)) simple enough that does not need to be enclosed in double quotes. It must follow a few rules: it contains only alphanumeric characters (and underscore) and it cannot start with a number.

#### Store (Service)
A [service](#service) implementing `Tinkwell.Store` that keeps track of all the [measures](#measure) in the [system](#system). It's responsible for converting between different units of measure, keeping historical values, and broadcasting changes to all subscribers. There should be only one source of truth in the system but, with the appropriate configuration, you can create multiple instances to distribute the workload; however, this is not supported out-of-the-box.

#### Supervisor
The entry-point of a Tinkwell application. It's in charge of parsing the [ensemble](#ensemble) configuration file, starting the required processes, and monitoring their health at the process level. There can be only one per machine and only one master per [system](#system).

#### System
One or more machines running a Tinkwell installation.

#### Watchdog
A [firmlet](#firmlet) that periodically queries all [services](#service) implementing `Tinkwell.HealthCheck` to report on their status.