# Custom runners

If you are not using an existing host (such as `GrpcHost`) to host your services and firmlets then you will need to create an executable (or a script). When writing a custom runner you can do whatever you want but remember:

- If you need to open an HTTPS endpoint then you should ask the Supervisor for the port to use:
    - Open a connection to the named pipe `tinkwell-supervisor-command-server` (or whatever specified in the setting `Supervisor:CommandServer:PipeName`). If your're using .NET then you could use `Tinkwell.Bootstrapper.Ipc.NamedPipeClient`, it's as simple as:
        ```cs
        async Task<int> ClaimPort(IConfiguration configuration)
        {
            var runnerName = Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable);
            var client = new NamedPipeClient();
            string? portNumber = await client.SendCommandToSupervisorAndDisconnectAsync(
                builder.Configuration, $"endpoints claim \"{Environment.MachineName}\" \"{runnerName}\"");

            if (string.IsNullOrWhiteSpace(portNumber))
                throw new InvalidOperationException("...");

            return int.Parse(portNumber, CultureInfo.InvariantCulture);
        }
        ```
    - Send a command `endpoints claim <machine name> <runner name>`. The name of the runner is available in the environment variable `TINKWELL_RUNNER_NAME`.
    - Once you obtained the port to use, you can optionally send `endpoints query <runner name>` to obtain the full host name (for example `https://my-computer:5000`).
    - Send the `exit` command.
- When your application is fully initialised and running, you must send a `signal <runner name>` command (followed by `exit`) to the Supervisor. If you omit this step then your runner cannot MUST be marked with `activation: mode=non_blocking` otherwise it'll cause the Supervisor to hung or timeout.
- You should monitor your parent process (its PID is in the environment variable `TINKWELL_SUPERVISOR_PID`). Often when the supervisor itself crashed it's good practice to gracefully shutdown as soon as possible. If you're using .NET you can use the `Tinkwell.Bootstrapper.Ipc.ParentProcessWatcher` helper class, like so:
    ```cs
    using var watcher = ParentProcessWatcher.WhenNotFound(() =>
    {
        // Parent process has been terminated
    });
    ```
    Optionally you could send a `ping` command (followed by `exit`, if successful) to check if the process is still running (for example if it has been respawn). 
- If you need the Discovery service and you're not in .NET then you need to:
    - Send the command `roles query tinkwell-discovery-service`, this is the address for the runner hosting the Discovery service.
    - Send the command `exit`.
    - From now on you must use that host to contact the gRPC Discovery Service to find all the other services (or to register your own services).
- If you want your service to be monitored for health (by the Watchdog) then you need to expose a `Tinkwell.HealthCheck` service (and use your own metrics to determine its status).

## Important

If you're using a .NET language then there are very few reasons to write your own runner (mostly .NET version incompatibilities). If you're using another language (Java, Python, Rust, C++, etc) you should consider invoking the [command line tool](./CLI.md) `tw` instead of interacting directly with the supervisor to register and to resolve the Discovery Service address (you can even subscribe for changes to a set of measures, if that's all you need).