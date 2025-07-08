using System.Reflection;

namespace Tinkwell.Bootstrapper.IO;

public static class IoHelpers
{
    public static string ResolveFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Directory.GetCurrentDirectory();

        if (Path.IsPathFullyQualified(path))
            return path;

        return Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    public static string GetCurrentDirectoryButPreferSameAs(string path)
    {
        if (Path.IsPathFullyQualified(path))
            return Path.GetDirectoryName(path)!;

        return Directory.GetCurrentDirectory();
    }

    public static string GetEntryAssemblyDirectoryName()
        => Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;

    public static string GetExecutingAssemblyDirectoryName()
        => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
}