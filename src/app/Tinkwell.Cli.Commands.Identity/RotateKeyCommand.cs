using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Identity;

public sealed class RotateKeySettings : TwSettings
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

[CliCommand("identity", "rotate-key", Description = "Rotate your API key on a Tinkwell service")]
public sealed class RotateKeyCommand : AsyncCommand<RotateKeySettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, RotateKeySettings settings, CancellationToken ct)
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
            if (!AnsiConsole.Confirm("This will invalidate your current API key. Continue?", false))
            {
                output.WriteWarning("Cancelled.");
                return 1;
            }
        }

        using var http = IdentityHttpClient.Create(url, settings.TimeoutSeconds);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var result = await output.RunWithStatusAsync("Rotating key...", async () =>
        {
            var response = await http.PostAsJsonAsync("authors/me/rotate-key", new { }, JsonOpts, ct);
            await IdentityHttpClient.EnsureSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
        });

        if (result.TryGetProperty("apiKey", out var newKeyEl))
        {
            var newKey = newKeyEl.GetString();

            if (output.Format == OutputFormat.Jsonl)
            {
                output.WriteSuccess(newKey ?? "");
            }
            else
            {
                output.WriteSuccess("API key rotated successfully!");
                AnsiConsole.MarkupLine("[bold yellow]New API Key (save it now — it won't be shown again!):[/]");
                output.WriteLine($"  {newKey}");
            }
        }
        else
        {
            output.WriteSuccess("API key rotated (no key returned in response).");
        }

        return 0;
    }
}
