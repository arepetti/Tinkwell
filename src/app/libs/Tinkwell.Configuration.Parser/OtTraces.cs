using System.Diagnostics;

namespace Tinkwell.Configuration.Parser;

internal static class OtTraces
{
    public const string SourceName = "Tinkwell.Configuration";
    public static readonly ActivitySource Source = new(SourceName);

    public const string Parse = "config.parse";
    public const string Include = "config.include";

    public const string ConfigPath = "config.path";
    public const string IncludePath = "include.path";
}
