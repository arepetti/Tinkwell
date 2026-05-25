using System.ComponentModel;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Coordinator;

[Description("Check if the coordinator is reachable")]
internal sealed class PingCommand : AsyncCommand<TwCoordinatorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TwCoordinatorSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            var result = await output.RunWithStatusAsync(
                $"Pinging coordinator at [magenta]{settings.PipeName}[/]...",
                () => PipeCommandRunner.SendAsync(settings, "runners list", ct));

            if (result.IsOk)
            {
                if (output.Format == OutputFormat.Jsonl)
                {
                    if (output.NonInteractive)
                        Console.WriteLine($$"""{"reachable":true,"latencyMs":{{result.Latency.TotalMilliseconds:F0}}}""");
                    else
                        output.WriteMarkup($"[green]Coordinator reachable[/] [dim]([cyan]{result.Latency.TotalMilliseconds:F0}[/]ms)[/]");
                }
                else
                {
                    output.WriteMarkup($"[green]Coordinator reachable[/] [dim]([cyan]{result.Latency.TotalMilliseconds:F0}[/]ms)[/]");
                }
                return 0;
            }

            output.WriteError($"Coordinator responded with error: {result.Message}");
            return 1;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
        {
            if (output.NonInteractive)
                Console.WriteLine($$"""{"reachable":false,"error":"{{ex.Message.Replace("\"", "\\\"")}}"}""");
            else
                output.WriteError($"Coordinator not reachable: {ex.Message}");
            return 1;
        }
    }
}
