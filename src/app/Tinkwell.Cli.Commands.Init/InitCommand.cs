using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Init;

public sealed class InitSettings : TwSettings
{
    [Description("Wizard pack name or directory")]
    [CommandOption("--pack|-p")]
    public string? Pack { get; set; }

    [Description("Output path override for the primary file")]
    [CommandOption("--output|-o")]
    public string? Output { get; set; }

    [Description("Overwrite existing files without prompting")]
    [CommandOption("--force")]
    [DefaultValue(false)]
    public bool Force { get; set; }

    [Description("Preview generated files without writing")]
    [CommandOption("--dry-run")]
    [DefaultValue(false)]
    public bool DryRun { get; set; }

    [Description("List available wizard packs and exit")]
    [CommandOption("--list-packs")]
    [DefaultValue(false)]
    public bool ListPacks { get; set; }

    [Description("Additional directory to search for packs")]
    [CommandOption("--pack-path")]
    public string? PackPath { get; set; }
}

[CliCommand(null, "init", Description = "Generate configuration files from a wizard pack")]
public sealed class InitCommand : AsyncCommand<InitSettings>
{
    [RequiresUnreferencedCode("Wizard validation uses reflection-based expression evaluation.")]
    public override async Task<int> ExecuteAsync(
        CommandContext context, InitSettings settings, CancellationToken ct)
    {
        if (settings.NonInteractive)
            throw new TwCommandException(
                "Non-interactive mode is not supported for 'tw init'. " +
                "Answer-file support is planned for a future release.");

        var console = AnsiConsole.Console;
        var catalog = new WizardPackCatalog(settings.PackPath);

        if (settings.ListPacks)
            return ListPacks(console, catalog);

        var packDir = ResolvePackDirectory(catalog, settings.Pack);

        console.MarkupLine($"[bold]Loading pack:[/] {Markup.Escape(Path.GetFileName(packDir))}");

        var pack = await WizardPackParser.LoadAsync(packDir, ct);

        console.MarkupLine($"[cyan]{Markup.Escape(pack.Title)}[/]");
        if (pack.Description is not null)
            console.MarkupLine($"[dim]{Markup.Escape(pack.Description)}[/]");
        console.WriteLine();

        var session = new WizardSession(console);
        var answers = await session.RunAsync(pack.Questions, ct);
        var writer = new GeneratedFileWriter(console, settings.Force, settings.DryRun);

        console.WriteLine();

        int filesWritten = 0;
        foreach (var output in pack.Outputs)
        {
            if (!answers.EvaluateCondition(output.WhenCondition))
                continue;

            var templatePath = Path.Combine(pack.PackDirectory, output.RenderTemplate);
            if (!File.Exists(templatePath))
            {
                console.MarkupLine(
                    $"[yellow]Warning:[/] Template not found: {Markup.Escape(templatePath)}");
                continue;
            }

            var rendered = await TemplateRenderer.RenderAsync(templatePath, answers, ct);
            rendered = NormalizeOutput(rendered);

            var diagnostics = await GeneratedFileValidator.ValidateAsync(
                output.Validator, rendered, ct);

            if (diagnostics.Count > 0)
            {
                console.MarkupLine(
                    $"[yellow]Validation warnings for {Markup.Escape(output.Path)}:[/]");
                foreach (var d in diagnostics)
                    console.MarkupLine($"  [dim]{Markup.Escape(d)}[/]");
            }

            var outputPath = string.Equals(output.Path, pack.PrimaryOutput, StringComparison.Ordinal)
                ? settings.Output ?? output.Path
                : output.Path;

            if (writer.Write(outputPath, rendered, null))
                filesWritten++;
        }

        console.WriteLine();
        console.MarkupLine(filesWritten > 0
            ? $"[green]Done![/] {filesWritten} file(s) generated."
            : "[yellow]No files were generated.[/]");

        return 0;
    }

    private static int ListPacks(IAnsiConsole console, WizardPackCatalog catalog)
    {
        var dirs = catalog.DiscoverPackDirectories();
        if (dirs.Count == 0)
        {
            console.MarkupLine("[yellow]No wizard packs found.[/]");
            return 0;
        }

        console.MarkupLine("[bold]Available packs:[/]");
        foreach (var dir in dirs)
            console.MarkupLine($"  [cyan]{Markup.Escape(Path.GetFileName(dir))}[/]");

        return 0;
    }

    private static string ResolvePackDirectory(WizardPackCatalog catalog, string? packName)
    {
        if (!string.IsNullOrWhiteSpace(packName))
        {
            if (Directory.Exists(packName)
                && File.Exists(Path.Combine(packName, "package.tw")))
                return Path.GetFullPath(packName);

            var found = catalog.FindPackDirectory(packName);
            if (found is not null)
                return found;

            throw new TwCommandException($"Pack '{packName}' not found.");
        }

        var all = catalog.DiscoverPackDirectories();
        return all.Count switch
        {
            0 => throw new TwCommandException(
                "No wizard packs found. Use --pack-path to specify a pack directory."),
            1 => all[0],
            _ => throw new TwCommandException(
                "Multiple packs available. Use --pack to select one, " +
                "or --list-packs to see available packs.")
        };
    }

    private static string NormalizeOutput(string text)
    {
        text = text.Replace("\r\n", "\n");

        while (text.Contains("\n\n\n"))
            text = text.Replace("\n\n\n", "\n\n");

        if (!text.EndsWith('\n'))
            text += "\n";

        return text;
    }
}
