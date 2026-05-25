using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Actions.Abstractions;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;

namespace Tinkwell.Runlet.Actions.Handlers;

/// <summary>
/// Built-in handler that sends an HTTP POST (or other method) to a URL
/// when an action fires.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>url</c> (required) — the target URL, supports expressions.</item>
///   <item><c>body</c> (optional) — the request body, supports expressions with
///     <c>format()</c>. Defaults to an empty body.</item>
///   <item><c>content-type</c> (optional) — media type. Defaults to <c>application/json</c>.</item>
///   <item><c>method</c> (optional) — HTTP method. Defaults to <c>POST</c>.</item>
///   <item><c>authorization</c> (optional) — value for the <c>Authorization</c> header
///     (e.g. <c>"Bearer abc123"</c>).</item>
/// </list>
/// <para>Example in a <c>.tw</c> configuration file:</para>
/// <code>
/// action notify-excursion {
///     source = signals
///     verb = fired
///
///     do http-post {
///         url = "https://hooks.example.com/alerts"
///         body = (format("{\"signal\":\"{Name}\",\"severity\":\"{severity}\"}"))
///         content-type = "application/json"
///     }
/// }
/// </code>
/// </remarks>
internal sealed class HttpPostActionHandler : IActionHandler
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly ILogger<HttpPostActionHandler> _logger;
    private readonly HttpClient _httpClient;

    public HttpPostActionHandler(ILogger<HttpPostActionHandler> logger)
        : this(logger, SharedClient)
    {
    }

    internal HttpPostActionHandler(ILogger<HttpPostActionHandler> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public string Name => "http-post";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var url = await ActionParameterResolver.ResolveRequiredAsync(
            "url", parameters, trigger, evaluator, ct);

        var body = await ActionParameterResolver.ResolveOptionalAsync(
            "body", parameters, trigger, evaluator, ct);

        var contentType = await ActionParameterResolver.ResolveOptionalAsync(
            "content-type", parameters, trigger, evaluator, ct)
            ?? "application/json";

        var methodStr = await ActionParameterResolver.ResolveOptionalAsync(
            "method", parameters, trigger, evaluator, ct)
            ?? "POST";

        var authorization = await ActionParameterResolver.ResolveOptionalAsync(
            "authorization", parameters, trigger, evaluator, ct);

        using var request = new HttpRequestMessage(new HttpMethod(methodStr), url);

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, contentType);

        if (authorization is not null)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        _logger.LogDebug("http-post: {Method} {Url}", methodStr, url);

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "http-post: {Method} {Url} returned {StatusCode}: {Body}",
                methodStr, url, (int)response.StatusCode, responseBody);
        }
    }
}
