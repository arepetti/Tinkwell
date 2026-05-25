using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

internal sealed class StoreDeleteSettings : StoreSettings
{
    [Description("Key to delete")]
    [CommandArgument(0, "<key>")]
    public string Key { get; set; } = "";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(BucketId))
            return ValidationResult.Error("--bucket-id is required for delete");
        return ValidationResult.Success();
    }
}

[Description("Delete a value from the state store")]
internal sealed class StoreDeleteCommand : AsyncCommand<StoreDeleteSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, StoreDeleteSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                $"Deleting [cyan]{settings.Key}[/]...",
                () => handle.Client.DeleteAsync(
                    new Tinkwell.Runlet.Store.Grpc.V1.DeleteRequest
                    {
                        BucketId = settings.BucketId!,
                        KeyNamespace = settings.Namespace ?? "",
                        Key = settings.Key
                    }, cancellationToken: ct).ResponseAsync);

            if (response.Found)
                output.WriteSuccess($"Deleted [cyan]{Markup.Escape(settings.Key)}[/]");
            else
                output.WriteWarning($"Key [cyan]{Markup.Escape(settings.Key)}[/] not found");

            return 0;
        }
        catch (TwCommandException ex)
        {
            output.WriteError(ex.Message);
            return 1;
        }
    }
}
