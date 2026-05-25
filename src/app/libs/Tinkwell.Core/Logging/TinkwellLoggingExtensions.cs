using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Tinkwell.Logging;

/// <summary>
/// Extension methods to register the <see cref="TinkwellConsoleFormatter"/>
/// as the default console log formatter.
/// </summary>
public static class TinkwellLoggingExtensions
{
    /// <summary>
    /// Adds console logging with the Tinkwell compact formatter.
    /// Override with <c>"Logging:Console:FormatterName"</c> set to
    /// <c>"simple"</c> or <c>"systemd"</c> in configuration to switch
    /// back to a built-in formatter.
    /// </summary>
    public static ILoggingBuilder AddTinkwellConsole(this ILoggingBuilder builder)
    {
        builder.AddConsole(options =>
        {
            options.FormatterName = TinkwellConsoleFormatter.FormatterName;
        });

        builder.AddConsoleFormatter<TinkwellConsoleFormatter, ConsoleFormatterOptions>();

        return builder;
    }
}
