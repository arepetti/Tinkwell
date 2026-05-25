using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("Show local system information (version, runtime, OS, plugin paths)")]
internal sealed class InfoCommand : AsyncCommand<TwSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public override Task<int> ExecuteAsync(
        CommandContext context, TwSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var productVersion = typeof(AppConfigurator).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        var plusIndex = productVersion.IndexOf('+');
        var displayVersion = plusIndex >= 0 ? productVersion[..plusIndex] : productVersion;

        var runtimeVersion = Environment.Version.ToString();
        var osDescription = RuntimeInformation.OSDescription;
        var architecture = RuntimeInformation.ProcessArchitecture.ToString();
        var baseDirectory = AppContext.BaseDirectory;

        if (output.Format == OutputFormat.Jsonl)
        {
            object payload;
            if (output.Verbose)
            {
                var pluginRoots = PluginCatalog.GetDefaultPluginRoots();
                var extensions = ExtensionScanner.Scan();
                payload = new
                {
                    productVersion = displayVersion,
                    runtime = runtimeVersion,
                    os = osDescription,
                    architecture,
                    baseDirectory,
                    pluginRoots,
                    extensions,
                };
            }
            else
            {
                payload = new
                {
                    productVersion = displayVersion,
                    runtime = runtimeVersion,
                    os = osDescription,
                    architecture,
                    baseDirectory,
                };
            }

            var json = JsonSerializer.Serialize(payload, JsonOpts);

            if (output.NonInteractive)
                Console.WriteLine(json);
            else
                output.WriteRawJsonColored(json);

            return Task.FromResult(0);
        }

        AnsiConsole.MarkupLine("[bold underline]Info[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [dim]Product version:[/] [cyan]{Markup.Escape(displayVersion)}[/]");
        AnsiConsole.MarkupLine($"  [dim].NET runtime:[/]    {Markup.Escape(runtimeVersion)}");
        AnsiConsole.MarkupLine($"  [dim]OS:[/]              {Markup.Escape(osDescription)}");
        AnsiConsole.MarkupLine($"  [dim]Architecture:[/]     {Markup.Escape(architecture)}");
        AnsiConsole.MarkupLine($"  [dim]App directory:[/]    [magenta]{Markup.Escape(baseDirectory)}[/]");

        if (output.Verbose)
        {
            var pluginRoots = PluginCatalog.GetDefaultPluginRoots();
            var extensions = ExtensionScanner.Scan();

            if (pluginRoots.Count > 0)
            {
                AnsiConsole.MarkupLine($"  [dim]Plugin roots:[/]");
                foreach (var root in pluginRoots)
                {
                    var exists = Directory.Exists(root);
                    var marker = exists ? "[green]exists[/]" : "[dim]not found[/]";
                    AnsiConsole.MarkupLine($"    [dim]-[/] [magenta]{Markup.Escape(root)}[/] ({marker})");
                }
            }

            if (extensions.Count > 0)
            {
                AnsiConsole.MarkupLine($"  [dim]Extensions:[/]");
                foreach (var ext in extensions)
                    AnsiConsole.MarkupLine($"    [dim]-[/] {Markup.Escape(ext)}");
            }
            else
            {
                AnsiConsole.MarkupLine("  [dim]Extensions:[/]      none");
            }
        }

        return Task.FromResult(0);
    }
}
