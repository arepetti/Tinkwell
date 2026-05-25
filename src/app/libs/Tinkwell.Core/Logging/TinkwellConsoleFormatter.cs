using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Tinkwell.Logging;

/// <summary>
/// A compact, colored console log formatter. Output format:
/// <c>HH:mm:ss.fff level - Category: message</c>
/// </summary>
public sealed class TinkwellConsoleFormatter(IOptions<ConsoleFormatterOptions> options)
    : ConsoleFormatter(FormatterName)
{
    /// <summary>The name used to register this formatter with the logging infrastructure.</summary>
    public const string FormatterName = "tinkwell";

    /// <inheritdoc/>
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter writer)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message))
            return;

        var fullCategory = logEntry.Category ?? "";
        var shortCategory = Shorten(fullCategory);
        var logLevel = Shorten(logEntry.LogLevel);

        if (logEntry.EventId.Id != 0)
            shortCategory += $"[{logEntry.EventId.Id}]";

        var now = _options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        writer.Write(now.ToString(_options.TimestampFormat ?? "HH:mm:ss.fff"));
        writer.Write(' ');

        var color = GetColor(logEntry.LogLevel);
        writer.Write($"{color}{logLevel}{Reset} - {shortCategory}: {message}");

        if (logEntry.Exception is not null)
        {
            writer.Write(' ');
            writer.Write(logEntry.Exception);
        }

        writer.WriteLine();

        if (_options.IncludeScopes && scopeProvider is not null)
        {
            scopeProvider.ForEachScope((scope, state) =>
            {
                state.WriteLine($"=> {scope}");
            }, writer);
        }
    }

    private const string Reset = "\x1b[0m";
    private const string Gray = "\x1b[37m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Red = "\x1b[31m";

    private readonly ConsoleFormatterOptions _options = options.Value;

    private static string Shorten(string category)
    {
        var lastDot = category.LastIndexOf('.');
        var name = lastDot >= 0 ? category[(lastDot + 1)..] : category;
        var tick = name.IndexOf('`');
        return tick >= 0 ? name[..tick] : name;
    }

    private static string Shorten(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "err ",
        LogLevel.Critical => "crit",
        _ => "????"
    };

    private static string GetColor(LogLevel level) => level switch
    {
        LogLevel.Trace => Gray,
        LogLevel.Debug => Gray,
        LogLevel.Information => Green,
        LogLevel.Warning => Yellow,
        LogLevel.Error or LogLevel.Critical => Red,
        _ => Reset
    };
}
