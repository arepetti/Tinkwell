namespace Tinkwell.Cli;

/// <summary>
/// Exception thrown when a CLI command fails. Caught by the global
/// exception handler to display a user-friendly error message.
/// </summary>
public sealed class TwCommandException(string message) : TinkwellException(message);
