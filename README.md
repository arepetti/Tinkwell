# Tinkwell

This repository is a work in progress and serves as an exploratory reference implementation for the IoT hub described in [IoT Architectures Under Pressure](https://dev.to/adriano-repetti/iot-architectures-under-pressure-why-implementation-isnt-as-simple-as-it-seems-part-1-3inn).

At this stage, the code is intended solely for **experimentation** with technical solutions: it is not production-ready, and both its structure and content may (should!) change drastically at any time.

## Idea

This approach is not useful only for IoT applications. A _system_ is composed of multiple _units_, each one dedicated to a specific task and it runs on its own isolated process. Units are launched by a _supervisor_ reading an _ensamble_ configuration file. Units communicates to each other using gRPC and they can find the available services through a _discovery_ service exposed by the supervisor.

The supervisor is in charge of launching and monitoring the health of all the units in the system but multiple sytems can cooperate in a _network_ and talk to each other without knowing where one specific is running.

Units should expose gRPC services but they should also support a "_file-like access_" to the resources they manage, this enables scripting through simple shell programs (and the shell itself can be a unit accepting HTTP POST requests for the script to execute, for example).

A minimal system.ensamble configuration file looks like this:

```
{% assign discovery_url = "https://localhost:5000"  %}

runner grpchost "Tinkwell.Bootstrapper.GrpcHost" {
    arguments: "--Kestrel:Endpoints:Http2:Url={{ discovery_url }}"
	service runner orchestrator "Tinkwell.Orchestrator.dll" {}
}

runner grpchost_store "Tinkwell.Bootstrapper.GrpcHost" {
    arguments: "--Kestrel:Endpoints:Http2:Url=https://localhost:5001 --Discovery:Master={{ discovery_url }}"
	service runner store "Tinkwell.Store.dll" {}
}

runner cpumonitor "Tinkwell.Bootstrapper.SensorHost" if "platform = 'linux'" {
    arguments: "--Discovery:Master={{ discovery_url }}"
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
}

runner some_firmware "Tinkwell.Bootstrapper.WasmHost"" {
    arguments: "--Discovery:Master={{ discovery_url }}"
    properties: {
        path: "some_firmware.wasm"
    }
}

runner another_native_firmware "./bin/another_firmware" {
    arguments: "--Discovery:Master={{ discovery_url }}"
}
```