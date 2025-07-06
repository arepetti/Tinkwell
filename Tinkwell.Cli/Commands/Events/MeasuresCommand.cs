using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Events;

[CommandFor("events")]
[Description("Publish and listen to events.")]
sealed class EventsCommand : Command<EventsCommand.Settings>
{
    public class Settings : CommonSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
