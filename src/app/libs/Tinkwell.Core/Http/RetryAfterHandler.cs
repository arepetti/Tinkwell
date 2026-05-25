using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace Tinkwell.Http;

/// <summary>
/// <see cref="DelegatingHandler"/> that automatically retries requests
/// receiving an HTTP 429 (Too Many Requests) response, honouring the
/// <c>Retry-After</c> header. Falls back to exponential backoff when
/// the header is absent.
/// </summary>
public sealed class RetryAfterHandler : DelegatingHandler
{
    /// <summary>Maximum number of retry attempts (default 3).</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Upper bound on any single retry delay (default 120 s).</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(120);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer request body so retries can re-send it. Without this,
        // stream-based or other one-shot content is consumed on the first
        // attempt and subsequent sends would transmit an empty body.
        byte[]? bodyBytes = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        for (int attempt=0; attempt <= MaxRetries; ++attempt)
        {
            if (attempt > 0 && bodyBytes is not null)
            {
                request.Content = new ByteArrayContent(bodyBytes);
                if (contentType is not null)
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            if (attempt == MaxRetries)
                return response;

            var delay = ParseRetryAfter(response)
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.FromSeconds(1);
            if (delay > MaxDelay)
                delay = MaxDelay;

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }

        throw new UnreachableException();
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null)
            return null;
        if (header.Delta.HasValue)
            return header.Delta.Value;
        if (header.Date.HasValue)
        {
            var wait = header.Date.Value - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1);
        }
        return null;
    }
}
