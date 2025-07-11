using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Json;
using Spectre.Console.Cli;
using Tinkwell.Bridge.MqttClient.Internal;
using System.Diagnostics;

namespace Tinkwell.Cli.Commands.Mqtt;

[CommandFor("match", parent: typeof(MqttCommand))]
[Description("Check the matching of a mapping file with a message.")]
sealed class MatchCommand : Command<MatchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TOPIC>")]
        public string Topic { get; set; } = "";

        [CommandArgument(1, "<PAYLOAD>")]
        public string Message { get; set; } = "";

        [CommandOption("-r|--rules")]
        [Description("Path to the mapping file to check against.")]
        public string Rules { get; set; } = "";

        [CommandOption("-j|--json")]
        [Description("Indicates whether the payload is a JSON string.")]
        public bool Json { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var parser = new MqttMessageParser(new() { Mapping = settings.Rules });

        if (!string.IsNullOrWhiteSpace(settings.Rules))
            AnsiConsole.MarkupLine($"Loaded [blueviolet]{parser.RuleCount}[/] rule(s) from [blueviolet]{settings.Rules}[/]");


        var (firstRun, average, matches) = TimeIt(() => parser.Parse(settings.Topic, settings.Message).ToList());
        AnsiConsole.MarkupLine($"Found [blueviolet]{matches.Count}[/] match(es)");
        AnsiConsole.MarkupLine($"First execution time [blueviolet]{firstRun}[/] ms");
        AnsiConsole.MarkupLine($"Average execution time [blueviolet]{average}[/] ms");

        PropertyValuesTable table = new PropertyValuesTable()
            .AddGroupTitle("MESSAGE")
            .AddEntry("Topic", settings.Topic);

        if (settings.Json)
            table.AddEntry("Payload", new JsonText(settings.Message));
        else
            table.AddEntry("Payload", settings.Message);

        AnsiConsole.Write(table);

        table = new PropertyValuesTable()
            .AddGroupTitle("MATCHES");

        if (matches.Count == 0)
            table.AddNote("No matches found for the given topic and payload.");

        foreach (var match in matches)
        {
            // Note that we call As***() and then cast to object because there could be conversions
            // when changing the type to double or string and here we want to see the end-result.
            table
                .AddNameEntry(match.Name)
                .AddEntry("Type", match.IsNumeric ? "number" : "string")
                .AddEntry("Value", match.IsNumeric ? (object)match.AsDouble() : (object)match.AsString())
                .AddRow();
        }

        AnsiConsole.Write(table);

        return ExitCode.Ok;
    }

    private static (string FirstRun, string Average, T Result) TimeIt<T>(Func<T> action)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var result = action();
        stopwatch.Stop();
        string firstRun = Math.Max(1, stopwatch.ElapsedMilliseconds).ToString();

        // Too few but it's just to have an idea
        int repetitions = stopwatch.ElapsedMilliseconds < 200 ? 10 : 3;

        stopwatch.Restart();
        for (int i=0; i < repetitions; ++i)
            action();
        stopwatch.Stop();
        long average = stopwatch.ElapsedMilliseconds / repetitions;
        return (firstRun, average == 0 ? "<1" : average.ToString(), result);
    }    
}

