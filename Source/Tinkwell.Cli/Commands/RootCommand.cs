using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Reflection;

namespace Tinkwell.Cli.Commands;

sealed class RootCommand : Command<RootCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-v|--version")]
        [Description("Prints version information")]
        public bool ShowVersion { get; set; }

        [CommandOption("--verbose")]
        [Description("Verbose output")]
        public bool Verbose { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.ShowVersion)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";
            var productVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "";

            if (settings.Verbose)
            {
                var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                AnsiConsole.WriteLine($"{productName} version {productVersion} ({informationalVersion})");
            }
            else
            {
                AnsiConsole.WriteLine($"{productName} version {productVersion}");
            }

            return ExitCode.Ok;
        }

        Console.WriteLine("Run with --help for usage.");
        return ExitCode.Ok;
    }
}
