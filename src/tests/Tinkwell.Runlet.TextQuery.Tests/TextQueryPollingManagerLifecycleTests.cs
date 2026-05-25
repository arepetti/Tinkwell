using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Runner;
using Tinkwell.Runlet.TextQuery;

namespace Tinkwell.Runlet.TextQuery.Tests;

public class TextQueryPollingManagerLifecycleTests
{
    private sealed class NopServiceDiscovery : IServiceDiscovery
    {
        public Task<ServiceDefinition?> DiscoverByNameAsync(
            string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceDefinition?>(null);

        public Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
            string? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceDefinition>>([]);

        public Task<T> CreateInstanceAsync<T>(ServiceDefinition service, CancellationToken cancellationToken = default)
            where T : class =>
            throw new NotSupportedException();
    }

    private sealed class TrackingTransport : ITextTransport
    {
        public bool Disposed { get; private set; }

        public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<string> QueryAsync(
            string? command, string lineTerminator, int timeoutMs, CancellationToken ct) =>
            Task.FromResult("");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StopAsync_DisposesAllTransportsInBag()
    {
        var manager = new TextQueryPollingManager(
            new TextQueryRunletOptions(null),
            new NopServiceDiscovery(),
            NullLogger<TextQueryPollingManager>.Instance);

        var field = typeof(TextQueryPollingManager).GetField(
            "_transports", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var bag = (ConcurrentBag<ITextTransport>)field.GetValue(manager)!;

        var first = new TrackingTransport();
        var second = new TrackingTransport();
        bag.Add(first);
        bag.Add(second);

        await manager.StopAsync(CancellationToken.None);

        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }
}
