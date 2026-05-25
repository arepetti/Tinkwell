using System.ComponentModel;
using Spectre.Console.Cli;
using SignalsGrpc = Tinkwell.Runlet.Signals.Grpc.V1;

namespace Tinkwell.Cli.Commands.Signals;

internal sealed class SignalsListSettings : SignalsSettings
{
}

[Description("List all registered signal definitions")]
internal sealed class SignalsListCommand : AsyncCommand<SignalsListSettings>
{
    private static readonly IReadOnlyList<ColumnDef<SignalsGrpc.SignalDefinitionProto>> Columns =
    [
        new("Name", s => s.Name),
        new("When", s => s.WhenExpression),
        new("Until", s => string.IsNullOrEmpty(s.UntilExpression) ? "-" : s.UntilExpression),
        new("For", s => string.IsNullOrEmpty(s.ForDuration) ? "-" : s.ForDuration),
        new("Parent", s => string.IsNullOrEmpty(s.ParentMeasure) ? "-" : s.ParentMeasure, VerboseOnly: true),
        new("Properties", s => s.Properties.Count > 0
            ? string.Join(", ", s.Properties.Select(p => $"{p.Key}={p.Value}"))
            : "-", VerboseOnly: true),
    ];

    public override async Task<int> ExecuteAsync(
        CommandContext context, SignalsListSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to signals service...",
                () => SignalsHelper.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                "Fetching signals...",
                () => handle.Client.ListAsync(
                    new SignalsGrpc.ListSignalsRequest(), cancellationToken: ct).ResponseAsync);

            var signals = response.Signals.ToList();
            output.WriteTable("Signals", Columns, signals);
            return 0;
        }
        catch (Grpc.Core.RpcException ex)
        {
            output.WriteError(ex.Status.Detail);
            return 1;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
