using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Supervisor;

[CommandFor("supervisor")]
[Description("Low level interface to the supervisor.")]
sealed class SupervisorCommand : Command<SupervisorCommand.Settings>
{
    public class Settings : CommonSettings
    {
        [CommandOption("-y|--confirm")]
        [Description("Performs the operation without asking for confirmation.")]
        public bool Confirmed { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
