using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands.Supervisor;

[CommandFor("runners")]
[Description("Manage the active runners.")]
sealed class RunnersCommand : Command<RunnersCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-m|--machine <MACHINE_NAME>")]
        [Description("The name of the machine where the Supervisor is listening.")]
        public string Machine { get; set; } = ".";

        [CommandOption("-p|--pipe <PIPE_NAME>")]
        [Description("The name of the pipe where the Supervisor is listening.")]
        public string Pipe { get; set; } = WellKnownNames.SupervisorCommandServerPipeName;

        [CommandOption("-t|--timeout <SECONDS>")]
        [Description("The timeout to wait for connections and replies.")]
        public int Timeout{ get; set; } = 5;
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}