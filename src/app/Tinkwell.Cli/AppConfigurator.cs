using System.Reflection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Batch;
using Tinkwell.Cli.Commands.Coordinator;
using Tinkwell.Cli.Commands.Events;
using Tinkwell.Cli.Commands.Measures;
using Tinkwell.Cli.Commands.Services;
using Tinkwell.Cli.Commands.Signals;
using Tinkwell.Cli.Commands.Store;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli;

internal static class AppConfigurator
{
    public static void Configure(IConfigurator config, ILogger logger)
    {
        config.SetApplicationName("tw");
        config.SetApplicationVersion(
            typeof(AppConfigurator).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0");

        config.AddCommand<RawCommand>("raw")
            .WithDescription("Send a raw command string to the coordinator pipe");

        config.AddCommand<StartCommand>("start")
            .WithDescription("Start the coordinator");

        config.AddCommand<QuitCommand>("quit")
            .WithDescription("Gracefully shut down the coordinator (use --wait to block until done)");

        config.AddCommand<PingCommand>("ping")
            .WithDescription("Check if the coordinator is reachable");

        config.AddCommand<StatusCommand>("status")
            .WithDescription("Show coordinator and runner summary");

        config.AddCommand<InfoCommand>("info")
            .WithDescription("Show local system information (version, runtime, OS, plugin paths)");

        config.AddCommand<UnblockCommand>("unblock")
            .WithDescription("Unblock runners waiting in the startup sequence");

        config.AddCommand<RunCommand>("run")
            .WithDescription("Execute a batch script file");

        config.AddCommand<IdCommand>("id")
            .WithDescription("Generate a new unique ID (guid or short)");

        config.AddBranch("runners", runners =>
        {
            runners.SetDescription("Manage runners");
            runners.AddCommand<RunnersListCommand>("list")
                .WithDescription("List all runners and their status");
            runners.AddCommand<RunnersHealthCommand>("health")
                .WithDescription("Show health status for all runners");
        });

        config.AddBranch("services", svc =>
        {
            svc.SetDescription("Query the service registry");
            svc.AddCommand<ServiceFindCommand>("find")
                .WithDescription("Find a service by name, alias, or family");
            svc.AddCommand<ServiceListCommand>("list")
                .WithDescription("List registered services");
        });

        config.AddBranch("store", store =>
        {
            store.SetDescription("Interact with the state store");
            store.AddCommand<StoreGetCommand>("get")
                .WithDescription("Get a value from the state store");
            store.AddCommand<StoreSetCommand>("set")
                .WithDescription("Set a value in the state store");
            store.AddCommand<StoreDeleteCommand>("delete")
                .WithDescription("Delete a value from the state store");
            store.AddCommand<StoreListCommand>("list")
                .WithDescription("List entries in the state store");
            store.AddCommand<StoreWatchCommand>("watch")
                .WithDescription("Watch the state store for changes");
        });

        config.AddBranch("measures", measures =>
        {
            measures.SetDescription("Interact with the measure registry");
            measures.AddCommand<MeasuresListCommand>("list")
                .WithDescription("List all measures");
            measures.AddCommand<MeasuresGetCommand>("get")
                .WithDescription("Get a single measure");
            measures.AddCommand<MeasuresSetCommand>("set")
                .WithDescription("Update a measure value");
            measures.AddCommand<MeasuresRegisterCommand>("register")
                .WithDescription("Register a new measure definition");
            measures.AddCommand<MeasuresWatchCommand>("watch")
                .WithDescription("Watch measures for value changes");
        });

        config.AddBranch("signals", signals =>
        {
            signals.SetDescription("Interact with the signal system");
            signals.AddCommand<SignalsCreateCommand>("create")
                .WithDescription("Create a new signal definition");
            signals.AddCommand<SignalsListCommand>("list")
                .WithDescription("List all registered signals");
            signals.AddCommand<SignalsWatchCommand>("watch")
                .WithDescription("Watch for signal events");
        });

        config.AddBranch("events", events =>
        {
            events.SetDescription("Interact with the event bus");
            events.AddCommand<EventsWatchCommand>("watch")
                .WithDescription("Watch for events");
            events.AddCommand<EventsPublishCommand>("publish")
                .WithDescription("Publish an event to the event bus");
        });

        // Tooling-only branch: Tinkwell.Studio and similar tools use
        // `config get-path` to locate the loaded ensemble. The branch and its
        // command are both hidden so they don't show up in `tw --help`.
        config.AddBranch("config", cfg =>
        {
            cfg.SetDescription("Configuration introspection (tooling-only)");
            cfg.AddCommand<ConfigGetPathCommand>("get-path")
                .WithDescription("Return the path of the ensemble configuration file")
                .IsHidden();
        });

        CommandLoader.RegisterExtensionCommands(config, logger);

        config.SetExceptionHandler((ex, _) =>
        {
            if (ex is TwCommandException)
                AnsiConsole.MarkupLine($"[red]Command error:[/] {Markup.Escape(ex.Message)}");
            else
                AnsiConsole.MarkupLine($"[red]Unknown error:[/] {Markup.Escape(ex.Message)}");
            return -1;
        });
    }
}
