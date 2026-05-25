using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands;

public sealed class IdSettings : TwSettings
{
    [Description("Kind of ID to generate: guid or short")]
    [CommandOption("--kind|-k")]
    [DefaultValue("guid")]
    public string Kind { get; set; } = "guid";
}

[Description("Generate a new unique ID")]
internal sealed class IdCommand : Command<IdSettings>
{
    public override int Execute(CommandContext context, IdSettings settings, CancellationToken ct)
    {
        var kind = settings.Kind.ToLowerInvariant();
        var id = kind switch
        {
            "short" => ShortIdGenerator.NewId(),
            "guid" => Guid.NewGuid().ToString(),
            _ => throw new TwCommandException($"Unknown kind '{settings.Kind}'. Use 'guid' or 'short'.")
        };

        var format = settings.ResolveFormat(OutputFormat.List);

        if (format == OutputFormat.Jsonl || settings.NonInteractive)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { kind, id }));
        }
        else
        {
            Console.WriteLine(id);
        }

        return 0;
    }
}
