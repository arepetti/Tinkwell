using Tinkwell.Measures.Storage;
using Tinkwell.TestHelpers;

namespace Tinkwell.Reducer.Tests;

public class ReducerTests
{
    public ReducerTests()
    {
        _storage = new InMemoryStorage();
        _storeAdapter = new InMemoryStoreAdapter(_storage);
        _fileReader = new MockTwmFileReader();
        _logger = new MockLogger<Reducer>();
        _options = new ReducerOptions { Path = "test.twm" };
    }

    [Fact]
    public async Task HappyPath_CalculatesAndSubscribesCorrectly()
    {
        // Arrange
        _fileReader.AddScalar("A", "1");
        _fileReader.AddScalar("B", "2");
        _fileReader.AddScalar("C", "A + B");
        var reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);

        // Act
        await reducer.StartAsync(default);

        // Assert
        var c = _storage.Find("C");
        Assert.NotNull(c);
        Assert.Equal(3, c.Value.AsDouble()); // 1 + 2

        await _storeAdapter.WriteQuantityAsync("A", 10, default);
        await Task.Delay(100); // Give time for the change to propagate
        Assert.Equal(12, _storage.Find("C")!.Value.AsDouble()); // 10 + 2

        await _storeAdapter.WriteQuantityAsync("B", 5, default);
        await Task.Delay(100); // Give time for the change to propagate
        Assert.Equal(15, _storage.Find("C")!.Value.AsDouble()); // 10 + 5
    }

    [Fact]
    public async Task NoDerivedMeasures_SitsIdle()
    {
        // Arrange
        var reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);

        // Act
        await reducer.StartAsync(default);

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Warning && l.Item2.Contains("No derived measures to calculate"));
    }

    [Fact]
    public async Task CircularDependency_LogsCriticalError()
    {
        // Arrange
        _fileReader.AddScalar("A", "B");
        _fileReader.AddScalar("B", "A");
        var reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);

        // Act
        await reducer.StartAsync(default);

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Critical && l.Item2.Contains("Circular dependency detected"));
    }

    [Fact]
    public async Task ConstantExpression_CalculatesOnceAndDoesNotSubscribe()
    {
        // Arrange
        _fileReader.AddScalar("C", "10 * 2");
        var reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);

        // Act
        await reducer.StartAsync(default);

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
        var reducer = new Reducer(_logger, _storeAdapter, _fileReader, _options);

        // Act
        await reducer.StartAsync(default);
        await _storeAdapter.WriteQuantityAsync("A", 10, default);
        await _storeAdapter.WriteQuantityAsync("B", 0, default); // Division by zero
        await Task.Delay(100);

        // Assert
        Assert.Contains(_logger.Logs, l => l.Item1 == Microsoft.Extensions.Logging.LogLevel.Error && l.Item2.Contains("Failed to recalculate"));
    }

    private readonly InMemoryStorage _storage;
    private readonly InMemoryStoreAdapter _storeAdapter;
    private readonly MockTwmFileReader _fileReader;
    private readonly MockLogger<Reducer> _logger;
    private readonly ReducerOptions _options;
}
