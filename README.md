# Tinkwell

This repository is a work in progress and serves as an exploratory reference implementation for the IoT hub described in [IoT Architectures Under Pressure](https://dev.to/adriano-repetti/iot-architectures-under-pressure-why-implementation-isnt-as-simple-as-it-seems-part-1-3inn).

At this stage, the code is intended solely for **experimentation** with technical solutions: it is not production-ready, and both its structure and content may (should!) change drastically at any time.

See [Setup.md](./Documentation/Setup.md) for instructions.

## What for?

This project does not contain the entire implementation of a "firmware-less Hub" (the Wasm host and library, UI and firmware repository won't be included for sure). That's because it can be used for different purposes then each specific use-case is going to be a separate repository.

You can find a blog post at [Tinkwell: Firmware-less IoT and Lab Automation](https://dev.to/adriano-repetti/tinkwell-firmware-less-iot-and-lab-automation-2gef), here a tiny extract:

* **Lab automation**: each lab instrument (whether it’s a spectrometer, thermal controller, or motion stage) often comes with its own driver or control script. Tinkwell can supervise each of these as a separate (monitored) runner process. To add a new device is as simple as adding a new `runner` entry in the Ensamble file.
    * With the Store module, you can track sensor readings (voltages, temperatures, concentrations) using unit-aware types. You can log conditions, perform derived computations and broadcast changes.
    * You could define workflows where launching a new test involves spinning up a sequence of runners: data collector, logger, analyzer, etc. Since runners can be composed hierarchically (one runner invoking others), you can build robust pipelines for repeatable experiments.
    * Using gRPC-based discovery and command interfaces, external systems—or even human operators—can see what's running and monitor the progress of an experiment.
* **Edge and Fog Computing in industrial IoT**: factories increasingly rely on edge devices to perform localized processing (e.g. anomaly detection from vibration sensors, temperature thresholds for safety cutoffs). Tinkwell offers a lightweight, resilient orchestration layer that doesn’t need containers or a full Kubernetes cluster—perfect for rugged industrial PCs at the edge.
* **Test benches and automated QA stations**: like in labs, automated testing environments in industrial R&D departments often involve specialized hardware setups. Scripts or binaries controlling signal generators, power supplies, or data loggers can be isolated into runners. Ensamble files could represent test configurations, while the store tracks metrics such as voltage, temperature, or system throughput.

## Idea

A _system_ is composed of multiple _units_, each one dedicated to a specific task and it runs on its own isolated process. Units are launched by a _supervisor_ reading an _ensamble_ configuration file. Units communicates to each other using gRPC and they can find the available services through a _discovery_ service exposed by the supervisor.

The supervisor is in charge of launching and monitoring the health of all the units in the system but multiple sytems can cooperate in a _network_ and talk to each other without knowing where one specific is running.

Units should expose gRPC services but they should also support a "_file-like access_" to the resources they manage, this enables scripting through simple shell programs (and the shell itself can be a unit accepting HTTP POST requests for the script to execute, for example).

A minimal `system.ensamble` configuration file looks like this:

```text
runner grpchost "Tinkwell.Bootstrapper.GrpcHost" {
	service runner orchestrator "Tinkwell.Orchestrator.dll" {}
    service runner "Tinkwell.HealthCheck.dll" {}
}

runner store "Tinkwell.Bootstrapper.GrpcHost" {
	service runner "Tinkwell.Store.dll" {}
    service runner "Tinkwell.HealthCheck.dll" {}
}

runner "Tinkwell.Bootstrapper.SensorHost" if "platform = 'linux'" {
    service runner cpu_temperature "Pi.Units.VcGend.dll" 
        properties: {
            command: "measure_temp"
            interval: "30s"
            device: "~/dev/cpu"
        }
    }
    service runner core_voltage "Pi.Units.VcGend.dll" 
        properties: {
            command: "measure_volts core"
            interval: "60s"
            device: "~/dev/voltage"
        }
    }
    service runner "Tinkwell.HealthCheck.dll" {}
}

runner watchdog "Tinkwell.Bootstrapper.DllHost" {
    service runner "Tinkwell.Watchdog.dll" {}
}

runner some_firmware "Tinkwell.Bootstrapper.WasmHost"" {
    properties: {
        path: "some_firmware.wasm"
    }
}

runner another_native_firmware "./bin/another_firmware" {
}
```

If you have derived measures then you could defined like so:

```text
// This is just a constant, we can use it to simulate changes forcing an update using PostMan
measure voltage {
	type: "ElectricPotential"
	unit: "Volt"
	expression: "5"
}

measure current {
	type: "ElectricCurrent"
	unit: "Ampere"
	expression: "2"
}

// This is a derived measure, it's recalculated when its dependencies change
measure power {
	type: "Power"
	unit: "Watt"
	expression: "voltage * current"
}
```

## What's Missing?

This code is just to explore an idea then, obviously, code quality has to vastly improve but there are a few bits that we surely need for an MVP:

* We have a VERY basic watchdog, now we need something to "act" on that. Broadcasting alerts/news? Running a local script? A new "host" that executes scripts based on events broadcasted through the alerts/news service (this could also work in tandem with alerts from the Trigger firmlet)
* Alerts: could be simple flags calculated when a condition is met (see the previous example for derived measures).
* A simple scripting mechanism and command line tools.
* A very simple web UI to manage the system, monitor its health, see logs and read measures (no dashboards for now!).
* Pluggable strategies (for example to select how to store measures, how to do load balancing when fetching a service by family name, etc).
* Update the store to support plain text data!!!
* All the other predefined runners/services described in the blog post!
