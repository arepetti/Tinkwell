using System.ComponentModel;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands.Templates.Manifest;

namespace Tinkwell.Cli.Commands.Templates;

[CommandFor("update", parent: typeof(TemplatesCommand))]
[Description("Update the list of templates from the remote registry.")]
public sealed class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
{
    public sealed class Settings : TemplatesCommand.Settings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await TemplateManifest.DownloadRemoteTemplatesAsync();
        return ExitCode.Ok;
    }
}

