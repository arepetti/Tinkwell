using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Tinkwell.Cli;

namespace Tinkwell.Cli.Commands.Identity;

internal static class IdentityHttpClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates an <see cref="HttpClient"/> backed by <see cref="IHttpClientFactory"/>
    /// with standard resilience (retry, circuit breaker, timeout).
    /// </summary>
    public static HttpClient Create(string baseUrl, int timeoutSeconds)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("Identity", http =>
        {
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }).AddStandardResilienceHandler();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient("Identity");
    }

    /// <summary>
    /// Reads the response body and throws a <see cref="TwCommandException"/>
    /// with the server error message if the response is not successful.
    /// </summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        string message;

        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
            message = error.TryGetProperty("detail", out var detail)
                ? detail.GetString() ?? body
                : error.TryGetProperty("message", out var msg)
                    ? msg.GetString() ?? body
                    : body;
        }
        catch
        {
            message = string.IsNullOrWhiteSpace(body)
                ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                : body;
        }

        throw new TwCommandException(message);
    }
}
