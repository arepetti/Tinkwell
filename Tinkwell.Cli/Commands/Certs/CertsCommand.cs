using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Certs;

[CommandFor("certs")]
[Description("Manage local self-signed certificates.")]
public sealed class CertsCommand : Command<CertsCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
        => 0;
}
