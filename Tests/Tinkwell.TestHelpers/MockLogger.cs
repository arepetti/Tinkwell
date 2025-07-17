
using Microsoft.Extensions.Logging;

namespace Tinkwell.TestHelpers;

public sealed class MockLogger<T> : ILogger<T>
{
    public List<(LogLevel, string)> Logs { get; } = new List<(LogLevel, string)>();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add((logLevel, formatter(state, exception)));
    }
}
