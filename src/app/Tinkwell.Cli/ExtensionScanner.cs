namespace Tinkwell.Cli;

/// <summary>
/// Scans <see cref="AppContext.BaseDirectory"/> for loaded CLI command extension DLLs.
/// </summary>
internal static class ExtensionScanner
{
    private static readonly string Prefix = typeof(Commands.CliCommandAttribute).Namespace! + ".";

    public static List<string> Scan()
    {
        var dir = AppContext.BaseDirectory;
        var result = new List<string>();

        if (!Directory.Exists(dir))
            return result;

        foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && name.Length > Prefix.Length)
            {
                result.Add(name);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
