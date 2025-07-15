# How-To

After you have your system up and running (see [Getting Started](./Getting-Started.md)) you might want to...

**Table of Contents**

*   [Define Measures](#define-measures)
    *   [Derived Measures](#derived-measures)
    *   [Constants](#constants)
    *   [Declarations](#declarations)
    *   [With Code](#with-code)
        *   [Registration](#registration)
        *   [Write a value](#write-a-value)
*   [Define Alerts](#define-alerts)
    *   [Monitor a Measure From Code](#monitor-a-measure-from-code)
*   [Create a Runner](#create-a-runner)

## Define Measures

Usually measures are created by runners but there are cases where their definition is part of the configuration.

### Derived Measures

When they're [derived measures](./Derived-measures.md). For example:
```text
measure power {
    type: "Power"
    unit: "Watt"
    expression: "current * voltage"
}
```

### Constants

When they're constants and you use them as parameters: simply define them as derived measures and use an [expression](./Expressions.md) returning a constant. For example:

```text
measure air_flow {
    type: "VolumeFlow"
    unit: "CubicMeterPerSecond"
    expression: "1"
}
```

Constants cannot be changed after their creation, if you're using them as parameters for testing (or you need to change them at run-time) then you have two options (both to use only for testing and debugging):1) define them as declarations and set their initial value with `tw measures write` or 2) disable constants adding `use_constants: false` to the properties of `Tinkwell.Reducer.dll` in the ensamble configuration file.

### Declarations

When you're declaring them, for example to be filled by the [MQTT Bridge](./MQTT-Bridge.md): declare them as derived measures an leave `expression` empty. For example:

```text
measure current {
    type: "ElectricCurrent"
    unit: "Ampere"
}
```

### With Code

If you're defining them inside a runner then you have to call the [`tinkwell.store` service](./Services/tinkwell.store.md).

#### Registration

You always must register a measure before you can read/write a value:

```csharp
// locator is a ServiceLocator instance, obtain with DI 
using var store = await locator.FindStoreAsync(CancellationToken.None);
await store.Client.RegisterAsync(new()
{
    Definition = new()
    {
        Type = StoreDefinition.Types.Type.Number,
        Name = "power",
        QuantityType = "Power",
        Unit = "Watt"
    },
});
```

#### Write a value

After it has been registered you can use the simplified wrapper to write a value:

```csharp
await store.Client
    .AsFacade()
    .WriteQuantityAsync("power", 10, CancellationToken.None);
```

If you have the value in another (compatible) [unit of measure](./Units.md):

```csharp
await store.Client
    .AsFacade()
    .WriteQuantityAsync("power", "10000 Btu/h", CancellationToken.None);
```

## Define Alerts

See [derived measures](./Derived-measures.md) for details.

* When they monitor a derived measure you defined: declare a `signal` block inside the derived measure definition:
    ```text
    measure maximum_load {
        type: "Power"
        unit: "Watt"
        expression: "80"
    }

    measure power {
        type: "Power"
        unit: "Watt"
        expression: "voltage * current"

        signal high_load {
            when: "power > maximum_load"
        }
    }
    ```
* When they depends on multiple measures or you did not declare them yourself:
    ```text
    signal high_oil_temperature {
        when: "out_oil_temperature - in_oil_temperature > 30"
    }
    ```

### Monitor a Measure From Code

If you want to monitor a measure yourself then:

```csharp
// locator is a ServiceLocator instance, obtain with DI 
using var store = await locator.FindStoreAsync(CancellationToken.None);
using var subscription = _store.Client.Subscribe(new() { Name = "voltage" });
await foreach (var response in subscription.ResponseStream.ReadAllAsync())
{
    double oldValue = response.OldValue.NumberValue;
    double newValue = response.NewValue.NumberValue;
    Console.WriteLine($"{response.Name} changed from {oldValue} to {newValue}");
}
```

## Create a Runner

The best way to start is to use one of the predefined templates with `tw templates create`. Basically you simply need to define a _registrar_ where you register your DI services (and your _worker_). If you do not expose gRPC services then it's simply as this, you also see how to read properties defined in the ensamble configuration file and expose them as options:

```csharp
sealed class Registrar : IHostedDllRegistrar
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            services.AddSingleton(new YourOptionClass
            {
                SomeProperty = host.GetPropertyString("some_value", "default value"),
            });

            services.AddHostedService<Worker>();
        });
    }
}
```

Somewhere else define your _worker_ (where the things get done):

```csharp
sealed class Worker(Reducer reducer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

In `StartAsync()` you should only perform initialization tasks, if you block the bootstrapping process then the other runners won't be initialized and the Supervisor might decide to terminate your host. If you have to perform a long-running operation then you have a few options:

* Derive your worker from `BackgroundService`. It's perfect, for example, when you use a `PeriodicTimer` to cadence your work.
    ```csharp
    sealed class Worker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            => Task.CompletedTask; // Do some real work...
    }
    ````
* Use the helper class `CancellableLongRunningTask`, it's useful if you span multiple separate workers:
    ```csharp
    sealed class Worker : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _ingest_.StartAsync(IngestDataAsync);
            await _process.StartAsync(ProcessDataAsync);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _ingest_.StopAsync(cancellationToken);
            await _process.StopAsync(cancellationToken);
        }

        private readonly CancellableLongRunningTask _ingest_ = new();
        private readonly CancellableLongRunningTask _process = new();

        private Task IngestDataAsync(CancellationToken cancellationToken)
            => Task.CompletedTask; // Do some real work

        private Task ProcessDataAsync(CancellationToken cancellationToken)
            => Task.CompletedTask; // Do some real work
    }
    ```
