using Spectre.Console;

namespace Tinkwell.Cli;

static class Consoles
{
    public static IAnsiConsole Error
    {
        get
        {
            _error ??= AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(Console.Error),
            });

            return _error;
        }
    }

    private static IAnsiConsole? _error;
}