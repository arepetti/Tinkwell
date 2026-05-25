using System.ComponentModel;
using System.Net.Http.Headers;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Identity;

public sealed class DeleteAccountSettings : TwSettings
{
    [Description("Service base URL (e.g. https://registry.example.com)")]
    [CommandOption("--url")]
    public string? Url { get; set; }

    [Description("Current API key for authentication")]
    [CommandOption("--api-key")]
    public string? ApiKey { get; set; }

    [Description("HTTP timeout in seconds (default: 60)")]
    [CommandOption("--timeout")]
    [DefaultValue(60)]
    public int TimeoutSeconds { get; set; } = 60;
}

[CliCommand("identity", "delete-account", Description = "Soft-delete your account on a Tinkwell service")]
public sealed class DeleteAccountCommand : AsyncCommand<DeleteAccountSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context, DeleteAccountSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var url = settings.Url;
        var apiKey = settings.ApiKey;

        if (!settings.NonInteractive)
        {
            url ??= AnsiConsole.Ask<string>("Service URL:");
            apiKey ??= AnsiConsole.Ask<string>("API key:");
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("--url is required.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new TwCommandException("--api-key is required.");

        if (!settings.NonInteractive)
        {
            AnsiConsole.MarkupLine("[bold red]WARNING:[/] This will permanently delete your account.");
            AnsiConsole.MarkupLine("All your published packages/firmlets will be soft-deleted.");

            if (!AnsiConsole.Confirm("Are you sure you want to delete your account?", false))
            {
                output.WriteWarning("Cancelled.");
                return 1;
            }
        }

        using var http = IdentityHttpClient.Create(url, settings.TimeoutSeconds);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        await output.RunWithStatusAsync("Deleting account...", async () =>
        {
            var response = await http.DeleteAsync("authors/me", ct);
            await IdentityHttpClient.EnsureSuccessAsync(response, ct);
        });

        output.WriteSuccess("Account deleted.");
        return 0;
    }
}
