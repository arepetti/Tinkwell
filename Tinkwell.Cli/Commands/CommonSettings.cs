using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands;

public abstract class CommonSettings : CommandSettings
{
    [CommandOption("--machine <MACHINE_NAME>")]
    [Description("The name of the machine where the Supervisor is listening.")]
    public string Machine { get; set; } = ".";

    [CommandOption("--pipe <PIPE_NAME>")]
    [Description("The name of the pipe where the Supervisor is listening.")]
    public string Pipe { get; set; } = WellKnownNames.SupervisorCommandServerPipeName;

    [CommandOption("--timeout <SECONDS>")]
    [Description("The timeout to wait for connections and replies.")]
    public int Timeout { get; set; } = 5;
}
