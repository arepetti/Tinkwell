using Tinkwell.Runner;

namespace Tinkwell.Runner.Abstractions.Tests;

public class ServiceDiscoveryExtensionsTests
{
    private sealed class FakeDiscovery : IServiceDiscovery
    {
        public string? LastSearchQuery { get; private set; }
        public string? LastDiscoverByName { get; private set; }

        public IReadOnlyList<ServiceDefinition> SearchQueryResult { get; init; } = [];
        public ServiceDefinition? ByNameResult { get; init; }

        public Task<ServiceDefinition?> DiscoverByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            LastDiscoverByName = name;
            return Task.FromResult(
                ByNameResult is not null && string.Equals(ByNameResult.Name, name, StringComparison.Ordinal)
                    ? ByNameResult
                    : null);
        }

        public Task<IReadOnlyList<ServiceDefinition>> SearchByNamePartialMatchAsync(
            string? query, CancellationToken cancellationToken = default)
        {
            LastSearchQuery = query;
            return Task.FromResult(SearchQueryResult);
        }

        public Task<T> CreateInstanceAsync<T>(
            ServiceDefinition service, CancellationToken cancellationToken = default)
            where T : class =>
            throw new NotImplementedException();
    }

    private static ServiceDefinition Svc(string name) =>
        new(name, ServiceType.Grpc, null, null, [], "h:1", "http://h:1/x");

    [Fact]
    public async Task DiscoverAsync_UsesExactCoordinatorLookup()
    {
        var expected = Svc("alpha");
        var d = new FakeDiscovery
        {
            ByNameResult = expected
        };

        var found = await d.DiscoverAsync("alpha");

        Assert.Equal("alpha", d.LastDiscoverByName);
        Assert.Null(d.LastSearchQuery);
        Assert.Same(expected, found);
    }

    [Fact]
    public async Task DiscoverAsync_DoesNotUsePartialSearchResults()
    {
        var partial = Svc("first");
        var d = new FakeDiscovery
        {
            SearchQueryResult = [partial]
        };

        var found = await d.DiscoverAsync("q");

        Assert.Equal("q", d.LastDiscoverByName);
        Assert.Null(d.LastSearchQuery);
        Assert.Null(found);
    }

    [Fact]
    public async Task CreateInstanceAsync_ThrowsWhenNotFound()
    {
        var d = new FakeDiscovery
        {
            ByNameResult = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            d.CreateInstanceAsync<object>("missing"));
    }
}
