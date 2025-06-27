# Health Monitoring

## Default health checks for hosted services

Add this service to any `GrpcHost` host to enable basic health monitoring at process level. Note that a process, to be monitored, has to expose a gRPC service then it's supported out-of-the-box only for firmlets hosted by `GrpcHost` (the Watchdog could monitor directly processes running on the same machine but it's not currently supported).

To add this service:

```
runner some_name "Tinkwell.Bootstrapper.GrpcHost" {
    service runner "path/to/some_service" {}
    service runner "Tinkwell.HealthCheck.dll" {
        properties: {
            interval: 15
        }
    }
}
```

All the properties are optional (the `properties` block can be omitted). Available properties are:

* `interval`: number of seconds to wait between each health check. Minimum is 5, default value is 30. Note that it's not the same as the sampling frequency used by the watchdog.
* `samples`: number of samples to evaluate when determining the health status of the process. Minimum is 1, maximum is 10, default is 5.
* `ema_alpha`: alpha coefficient (in %) for the Expontential Moving Average applied to the acquired samples to calculate the average. Smaller values equal to smoother, slower response (higher weight to older samples). 100 to ignore the older samples and use only the last one, 0 to disable EMA and use a simple average instead. Default value is 70.
* `maximum_cpu_usage`: CPU usage (in %) used as threshold to determine if the process has degraded performance. When queried by the watchdog it reports `DEGRADED` performance if the average of the last `samples` is above this threshold. Default is 90.
---
If the basic logic implemented in this service is not enough for your needs then you can implement your own logic and expose a `Tinkwell.HealthCheck` service (see `protos/tinkwell.health_check.proto`), do not forget to register it with `HealthCheck` as `FamilyName`. This could be the case if you want to use OpenTelemetry, you could, for example, write your own exporter derived from `BaseExporter<Metric>`. Alternatively you could leave out this service and the watchdog entirely and use OpenTelemetry for everything.

You're going to need to expose your own service also if you're writing a [custom runner](Custom%20runners.md).

## Configuring the Watchdog

To add a `Tinkwell.HealthCheck` service to your `GrpcHost` is not enought to monitor its health, we need a _watcher_ to collect that data and to store it somewhere. This task is assigned to the Watchdog:

```
runner "Tinkwell.Bootstrapper.DllHost" {
    service runner watchdog "Tinkwell.Watchdog.dll" {
        properties: {
            interval: 60
        }
    }
}
```

All the properties are optional (the `properties` block can be omitted). Available properties are:

* `interval`: number of seconds to wait between each health check. Minimum is 5, default value is 60. Ideally this should be slower than the highest sampling frequency used for any `Tinkwell.HealthCheck.dll` instance.
* `name_pattern`: the pattern used to create the measure to save in the store. You can use the `"{{ name }}"` placeholder which corresponds to the name of the runner to which the measure refers to. Default value is `"__HealthCheck_{{ name }}__"`.
