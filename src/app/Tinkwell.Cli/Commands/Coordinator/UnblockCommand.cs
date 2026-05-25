using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("Unblock runners waiting in the startup sequence")]
internal sealed class UnblockCommand : AsyncCommand<TwCoordinatorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TwCoordinatorSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            await output.RunWithStatusAsync(
                "Sending unblock signal...",
                () => PipeCommandRunner.SendOkAsync(settings, "notify unblock", ct));

            output.WriteSuccess("Runners unblocked");
            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
