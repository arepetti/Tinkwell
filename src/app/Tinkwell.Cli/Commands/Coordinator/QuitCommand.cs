using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

internal sealed class QuitSettings : TwCoordinatorSettings
{
    [Description("Wait for the coordinator to shut down before returning")]
    [CommandOption("--wait|-w")]
    [DefaultValue(false)]
    public bool Wait { get; set; }
}

[Description("Gracefully shut down the coordinator")]
internal sealed class QuitCommand : AsyncCommand<QuitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, QuitSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var result = await output.RunWithStatusAsync(
            "Sending quit...",
            () => PipeCommandRunner.SendAsync(settings, "quit", ct));

        if (!result.IsOk)
        {
            output.WriteError(result.Message ?? "Quit command failed");
            return 1;
        }

        output.WriteSuccess(result.Message ?? "Coordinator is shutting down");

        if (!settings.Wait)
            return 0;

        await output.RunWithStatusAsync(
            "Waiting for coordinator to shut down...",
            () => WaitForShutdownAsync(settings));

        output.WriteSuccess("Coordinator has shut down");
        return 0;
    }

    private static async Task WaitForShutdownAsync(TwCoordinatorSettings settings)
    {
        for (int i=0; i < 120; ++i)
        {
            await Task.Delay(500);

            try
            {
                await PipeCommandRunner.SendAsync(settings, "ping");
            }
            catch
            {
                return;
            }
        }

        throw new TwCommandException("Timed out waiting for coordinator to shut down");
    }
}
