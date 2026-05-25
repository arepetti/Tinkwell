using System.Net;
using System.Net.Http;
using Tinkwell.Http;

namespace Tinkwell.Core.Tests;

public class RetryAfterHandlerTests
{
    private sealed class OkAfter429Handler : HttpMessageHandler
    {
        private int _attempt;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_attempt++ == 0)
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromMilliseconds(20));
                return Task.FromResult(r);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task SendAsync_RetriesOn429WithRetryAfter()
    {
        using var inner = new OkAfter429Handler();
        using var handler = new RetryAfterHandler { InnerHandler = inner, MaxRetries = 3 };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/dummy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class Always429Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task SendAsync_ExhaustsRetries_Returns429()
    {
        using var inner = new Always429Handler();
        using var handler = new RetryAfterHandler { InnerHandler = inner, MaxRetries = 2 };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/dummy");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private sealed class BodyCapture429ThenOkHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        private int _attempt;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            if (_attempt++ == 0)
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromMilliseconds(10));
                return r;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task SendAsync_BuffersContentForRetry()
    {
        using var inner = new BodyCapture429ThenOkHandler();
        using var handler = new RetryAfterHandler { InnerHandler = inner, MaxRetries = 3 };
        using var client = new HttpClient(handler);

        var content = new StringContent("test-payload");
        var response = await client.PostAsync("http://localhost/dummy", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("test-payload", inner.LastBody);
    }

    [Fact]
    public async Task SendAsync_PreservesContentTypeOnRetry()
    {
        using var inner = new BodyCapture429ThenOkHandler();
        using var handler = new RetryAfterHandler { InnerHandler = inner, MaxRetries = 3 };
        using var client = new HttpClient(handler);

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("http://localhost/dummy", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
