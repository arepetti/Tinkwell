using Tinkwell.Measures.Storage;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reducer.Tests;

public class ReducerTests : IAsyncLifetime
{
    private readonly TestInMemoryStorage _storage = new();
    private readonly InMemoryStoreAdapter _storeAdapter;
    private readonly MockTwmFileReader _fileReader = new();
    private readonly MockLogger<Reducer> _logger = new();
    private readonly ReducerOptions _options = new() { Path = "test.twm" };
    private Reducer _reducer = default!;

    public ReducerTests()
    {
        _storeAdapter = new InMemoryStoreAdapter(_storage);
    }

    public Task InitializeAsync()
    {
        _reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _reducer.DisposeAsync();
    }

    [Fact]
    public async Task HappyPath_CalculatesAndSubscribesCorrectly()
    {
        // Arrange
        _fileReader.AddScalar("A", "1");
        _fileReader.AddScalar("B", "2");
        _fileReader.AddScalar("C", "A + B");

        // Act
        await _reducer.StartAsync(default);
        await _reducer.WaitForSubscriptionReadyAsync();

        // Assert
        var c = _storage.Find("C");
        Assert.NotNull(c);
        Assert.Equal(3, c.Value.AsDouble()); // 1 + 2

        await _storeAdapter.WriteQuantityAsync("A", 10, default);
        await AsyncTestHelper.WaitForCondition(() => _storage.Find("C")?.Value.AsDouble() == 12, failureMessage: "Measure 'C' did not update to 12 after changing 'A'");
        Assert.Equal(12, _storage.Find("C")!.Value.AsDouble()); // 10 + 2

        await _storeAdapter.WriteQuantityAsync("B", 5, default);
        await AsyncTestHelper.WaitForCondition(() => _storage.Find("C")?.Value.AsDouble() == 15, failureMessage: "Measure 'C' did not update to 15 after changing 'B'");
        Assert.Equal(15, _storage.Find("C")!.Value.AsDouble()); // 10 + 5
    }

    [Fact]
    public async Task NoDerivedMeasures_SitsIdle()
    {
        // Act
        await _reducer.StartAsync(default);
        await _reducer.WaitForSubscriptionReadyAsync();

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Warning && l.Item2.Contains("No derived measures to calculate"));
    }

    [Fact]
    public async Task CircularDependency_LogsCriticalError()
    {
        // Arrange
        _fileReader.AddScalar("A", "B");
        _fileReader.AddScalar("B", "A");

        // Act
        await _reducer.StartAsync(default);
        await _reducer.WaitForSubscriptionReadyAsync();

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Critical && l.Item2.Contains("Circular dependency detected"));
    }

    [Fact]
    public async Task ConstantExpression_CalculatesOnceAndDoesNotSubscribe()
    {
        // Arrange
        _fileReader.AddScalar("C", "10 * 2");

        // Act
        await _reducer.StartAsync(default);
        await _reducer.WaitForSubscriptionReadyAsync();

        // Assert
        var c = _storage.Find("C");
        Assert.NotNull(c);
        Assert.Equal(20, c.Value.AsDouble());
        Assert.Equal(0, _storeAdapter.Subscribers);
    }

    [Fact]
    public async Task ExpressionFailure_LogsErrorAndDisablesMeasure()
    {
        // Arrange
        _fileReader.AddScalar("A", "0");
        _fileReader.AddScalar("B", "0");
        _fileReader.AddScalar("C", "A / B");

        // Act
        await _reducer.StartAsync(default);
        await _reducer.WaitForSubscriptionReadyAsync();
        await _storeAdapter.WriteQuantityAsync("A", 10, default);
        await _storeAdapter.WriteQuantityAsync("B", 0, default); // Division by zero
        await Task.Delay(100);

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Error && l.Item2.Contains("Failed to recalculate"));
    }
}
