namespace Tinkwell.Studio.Services;

public sealed class TwCliException : Exception
{
    public TwCliException(int exitCode, string command, string stderr)
        : base(FormatMessage(exitCode, command, stderr))
    {
        ExitCode = exitCode;
        Command = command;
        Stderr = stderr;
    }

    public int ExitCode { get; }

    public string Command { get; }

    public string Stderr { get; }

    private static string FormatMessage(int exitCode, string command, string stderr)
    {
        var trimmed = string.IsNullOrWhiteSpace(stderr) ? "(no stderr output)" : stderr.Trim();
        return $"`{command}` exited with code {exitCode}: {trimmed}";
    }
}
