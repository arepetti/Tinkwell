using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli.Commands.Identity;

public sealed class SignupSettings : TwSettings
{
    [Description("Service base URL (e.g. https://registry.example.com)")]
    [CommandOption("--url")]
    public string? Url { get; set; }

    [Description("Unique author handle (e.g. arepetti)")]
    [CommandOption("--handle")]
    public string? Handle { get; set; }

    [Description("Display name (e.g. \"Adriano Repetti\")")]
    [CommandOption("--public-name")]
    public string? PublicName { get; set; }

    [Description("Email address")]
    [CommandOption("--email")]
    public string? Email { get; set; }

    [Description("Company name (optional)")]
    [CommandOption("--company")]
    public string? CompanyName { get; set; }

    [Description("Path to the public key file (X.509 SPKI)")]
    [CommandOption("--author-key")]
    public string? AuthorKeyFile { get; set; }

    [Description("HTTP timeout in seconds (default: 60)")]
    [CommandOption("--timeout")]
    [DefaultValue(60)]
    public int TimeoutSeconds { get; set; } = 60;
}

[CliCommand("identity", "signup", Description = "Register a new author account on a Tinkwell service")]
public sealed class SignupCommand : AsyncCommand<SignupSettings>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context, SignupSettings settings, CancellationToken ct)
    {
        var output = new OutputContext(settings);

        var url = settings.Url;
        var handle = settings.Handle;
        var publicName = settings.PublicName;
        var email = settings.Email;
        var company = settings.CompanyName;
        var authorKeyFile = settings.AuthorKeyFile;

        if (!settings.NonInteractive)
        {
            url ??= AnsiConsole.Ask<string>("Service URL:");
            handle ??= AnsiConsole.Ask<string>("Handle:");
            publicName ??= AnsiConsole.Ask<string>("Public name:");
            email ??= AnsiConsole.Ask<string>("Email:");
            company ??= AnsiConsole.Ask<string>("Company name [dim](optional, press Enter to skip)[/]:", "");
            authorKeyFile ??= AnsiConsole.Ask<string>("Path to public key file:");

            if (string.IsNullOrWhiteSpace(company))
                company = null;
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new TwCommandException("--url is required.");
        if (string.IsNullOrWhiteSpace(handle))
            throw new TwCommandException("--handle is required.");
        if (string.IsNullOrWhiteSpace(publicName))
            throw new TwCommandException("--public-name is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new TwCommandException("--email is required.");
        if (string.IsNullOrWhiteSpace(authorKeyFile))
            throw new TwCommandException("--author-key is required.");

        if (!File.Exists(authorKeyFile))
            throw new TwCommandException($"Public key file not found: {authorKeyFile}");

        if (!settings.NonInteractive)
        {
            AnsiConsole.MarkupLine($"[bold]Service:[/]     {Markup.Escape(url)}");
            AnsiConsole.MarkupLine($"[bold]Handle:[/]      {Markup.Escape(handle)}");
            AnsiConsole.MarkupLine($"[bold]Public name:[/] {Markup.Escape(publicName)}");
            AnsiConsole.MarkupLine($"[bold]Email:[/]       {Markup.Escape(email)}");
            if (!string.IsNullOrWhiteSpace(company))
                AnsiConsole.MarkupLine($"[bold]Company:[/]     {Markup.Escape(company)}");

            if (!AnsiConsole.Confirm("Submit registration?", true))
            {
                output.WriteWarning("Cancelled.");
                return 1;
            }
        }

        var publicKeyBytes = await File.ReadAllBytesAsync(authorKeyFile, ct);
        var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);

        using var http = IdentityHttpClient.Create(url, settings.TimeoutSeconds);

        var body = new { handle, publicName, email, publicKey = publicKeyBase64, companyName = company };

        var result = await output.RunWithStatusAsync("Signing up...", async () =>
        {
            var response = await http.PostAsJsonAsync("authors/signup", body, JsonOpts, ct);
            await IdentityHttpClient.EnsureSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
        });

        if (result.TryGetProperty("apiKey", out var apiKeyEl))
        {
            var apiKey = apiKeyEl.GetString();

            if (output.Format == OutputFormat.Jsonl)
            {
                output.WriteSuccess(apiKey ?? "");
            }
            else
            {
                output.WriteSuccess("Account created successfully!");
                AnsiConsole.MarkupLine($"\n[bold yellow]Your API Key (save it now — it won't be shown again!):[/]");
                output.WriteLine($"  {apiKey}");
            }
        }
        else
        {
            output.WriteSuccess("Account created successfully!");
        }

        return 0;
    }
}
