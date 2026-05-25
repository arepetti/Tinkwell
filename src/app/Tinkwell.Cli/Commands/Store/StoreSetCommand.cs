using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Tinkwell.Cli.Commands.Store;

internal sealed class StoreSetSettings : StoreSettings
{
    [Description("Key to set")]
    [CommandArgument(0, "<key>")]
    public string Key { get; set; } = "";

    [Description("JSON value to store")]
    [CommandArgument(1, "<value>")]
    public string Value { get; set; } = "";

    [Description("Time-to-live in seconds (0 = permanent)")]
    [CommandOption("--ttl|-t")]
    [DefaultValue(0)]
    public int Ttl { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(BucketId))
            return ValidationResult.Error("--bucket-id is required for set");
        return ValidationResult.Success();
    }
}

[Description("Set a value in the state store")]
internal sealed class StoreSetCommand : AsyncCommand<StoreSetSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, StoreSetSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        try
        {
            using var handle = await output.RunWithStatusAsync(
                "Connecting to store...",
                () => StoreClient.ConnectAsync(settings, ct));

            var response = await output.RunWithStatusAsync(
                $"Setting [cyan]{settings.Key}[/]...",
                () => handle.Client.SetAsync(
                    new Tinkwell.Runlet.Store.Grpc.V1.SetRequest
                    {
                        BucketId = settings.BucketId!,
                        KeyNamespace = settings.Namespace ?? "",
                        Key = settings.Key,
                        Value = settings.Value,
                        TtlSeconds = settings.Ttl
                    }, cancellationToken: ct).ResponseAsync);

            var created = response.CreatedAt?.ToDateTime().ToString("u") ?? "-";
            var updated = response.UpdatedAt?.ToDateTime().ToString("u") ?? "-";

            output.WriteSuccess(
                $"Set [cyan]{Markup.Escape(settings.Key)}[/] (created: {created}, updated: {updated})");
            return 0;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
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
