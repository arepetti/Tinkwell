using Tinkwell.Measures.History;

namespace Tinkwell.Measures.History.TimescaleDb.Tests;

public sealed class TimescaleDbMeasureHistoryStoreTests
{
    private const string ValidFormatConnectionString =
        "Host=127.0.0.1;Port=65432;Database=testdb;Username=test;Password=test;Timeout=3";

    [Fact]
    public void Constructor_with_null_options_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TimescaleDbMeasureHistoryStore((TimescaleDbOptions)null!));
    }

    [Fact]
    public async Task Constructor_with_connection_string_creates_valid_instance()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(ValidFormatConnectionString);

        Assert.NotNull(store);
    }

    [Fact]
    public async Task WriteAsync_with_null_point_throws_ArgumentNullException()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(
            new TimescaleDbOptions { ConnectionString = ValidFormatConnectionString, AutoCreateSchema = false });

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.WriteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task WriteManyAsync_with_null_list_throws_ArgumentNullException()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(
            new TimescaleDbOptions { ConnectionString = ValidFormatConnectionString, AutoCreateSchema = false });

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.WriteManyAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task WriteManyAsync_with_empty_list_returns_immediately_without_throwing()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(
            new TimescaleDbOptions { ConnectionString = ValidFormatConnectionString, AutoCreateSchema = false });

        await store.WriteManyAsync([], CancellationToken.None);
    }

    [Fact]
    public async Task QueryAsync_with_null_query_throws_ArgumentNullException()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(
            new TimescaleDbOptions { ConnectionString = ValidFormatConnectionString, AutoCreateSchema = false });

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.QueryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SyncDefinitionAsync_with_null_definition_throws_ArgumentNullException()
    {
        await using var store = new TimescaleDbMeasureHistoryStore(
            new TimescaleDbOptions { ConnectionString = ValidFormatConnectionString, AutoCreateSchema = false });

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SyncDefinitionAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task DisposeAsync_completes_without_error()
    {
        var store = new TimescaleDbMeasureHistoryStore(ValidFormatConnectionString);

        await store.DisposeAsync();
    }
}
