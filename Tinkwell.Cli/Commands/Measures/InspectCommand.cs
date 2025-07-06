using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Measures;

[CommandFor("inspect", parent: typeof(MeasuresCommand))]
[Description("Inspect a single measure.")]
sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
{
    public sealed class Settings : MeasuresCommand.Settings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the measure to inspect")]
        public string Name { get; set; } = "";

        [CommandOption("--value")]
        [Description("Include current value.")]
        public bool Value { get; set; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return ListCommand.List(new ListCommand.Settings
        {
            Search = settings.Name,
            Machine = settings.Machine,
            Pipe = settings.Pipe,
            Timeout = settings.Timeout,
            Values = settings.Value,
            Verbose = true,
        });
    }
}
