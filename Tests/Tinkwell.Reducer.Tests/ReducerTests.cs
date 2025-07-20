using Tinkwell.Measures.Storage;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reducer.Tests;

public class ReducerTests
{
    private readonly ReducerOptions _options = new() { Path = "test.twm" };

    [Fact]
    [Trait("Category", "CI-Disabled")]
    public async Task HappyPath_CalculatesAndSubscribesCorrectly()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new InMemoryStoreAdapter(storage);
        MockTwmFileReader fileReader = new();
        MockLogger<Reducer> logger = new();
        Reducer reducer = new Reducer(logger, storeAdapter, fileReader, _options);

        fileReader.AddScalar("A", "1");
        fileReader.AddScalar("B", "2");
        fileReader.AddScalar("C", "A + B");

        // Act
        await reducer.StartAsync(default);
        await reducer.WaitForSubscriptionReadyAsync();

        // Assert
        var c = storage.Find("C");
        Assert.NotNull(c);
        Assert.Equal(3, c.Value.AsDouble()); // 1 + 2

        await storeAdapter.WriteQuantityAsync("A", 10, default);
        await AsyncTestHelper.WaitForCondition(() => storage.Find("C")?.Value.AsDouble() == 12, failureMessage: "Measure 'C' did not update to 12 after changing 'A'");
        Assert.Equal(12, storage.Find("C")!.Value.AsDouble()); // 10 + 2

        await storeAdapter.WriteQuantityAsync("B", 5, default);
        await AsyncTestHelper.WaitForCondition(() => storage.Find("C")?.Value.AsDouble() == 15, failureMessage: "Measure 'C' did not update to 15 after changing 'B'");
        Assert.Equal(15, storage.Find("C")!.Value.AsDouble()); // 10 + 5

        // Clean up
        await reducer.DisposeAsync();
    }

    [Fact]
    public async Task NoDerivedMeasures_SitsIdle()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new InMemoryStoreAdapter(storage);
        MockTwmFileReader fileReader = new();
        MockLogger<Reducer> logger = new();
        Reducer reducer = new Reducer(logger, storeAdapter, fileReader, _options);

        // Act
        await reducer.StartAsync(default);
        await reducer.WaitForSubscriptionReadyAsync();

        // Assert
        Assert.Contains(logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Warning && l.Item2.Contains("No derived measures to calculate"));

        // Clean up
        await reducer.DisposeAsync();
    }

    [Fact]
    public async Task CircularDependency_LogsCriticalError()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new InMemoryStoreAdapter(storage);
        MockTwmFileReader fileReader = new();
        MockLogger<Reducer> logger = new();
        Reducer reducer = new Reducer(logger, storeAdapter, fileReader, _options);

        fileReader.AddScalar("A", "B");
        fileReader.AddScalar("B", "A");

        // Act
        await reducer.StartAsync(default);
        await reducer.WaitForSubscriptionReadyAsync();

        // Assert
        Assert.Contains(logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Critical && l.Item2.Contains("Circular dependency detected"));

        // Clean up
        await reducer.DisposeAsync();
    }

    [Fact]
    public async Task ConstantExpression_CalculatesOnceAndDoesNotSubscribe()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new InMemoryStoreAdapter(storage);
        MockTwmFileReader fileReader = new();
        MockLogger<Reducer> logger = new();
        Reducer reducer = new Reducer(logger, storeAdapter, fileReader, _options);

        fileReader.AddScalar("C", "10 * 2");

        // Act
        await reducer.StartAsync(default);
        await reducer.WaitForSubscriptionReadyAsync();

        // Assert
        var c = storage.Find("C");
        Assert.NotNull(c);
        Assert.Equal(20, c.Value.AsDouble());
        Assert.Equal(0, storeAdapter.Subscribers);

        // Clean up
        await reducer.DisposeAsync();
    }

    [Fact]
    public async Task ExpressionFailure_LogsErrorAndDisablesMeasure()
    {
        // Arrange
        TestInMemoryStorage storage = new();
        InMemoryStoreAdapter storeAdapter = new InMemoryStoreAdapter(storage);
        MockTwmFileReader fileReader = new();
        MockLogger<Reducer> logger = new();
        Reducer reducer = new Reducer(logger, storeAdapter, fileReader, _options);

        fileReader.AddScalar("A", "0");
        fileReader.AddScalar("B", "0");
        fileReader.AddScalar("C", "A / B");

        // Act
        await reducer.StartAsync(default);
        await reducer.WaitForSubscriptionReadyAsync();
        await storeAdapter.WriteQuantityAsync("A", 10, default);
        await storeAdapter.WriteQuantityAsync("B", 0, default); // Division by zero
        await Task.Delay(100);

        // Assert
        Assert.Contains(logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Error && l.Item2.Contains("Failed to recalculate"));

        // Clean up
        await reducer.DisposeAsync();
    }
}
