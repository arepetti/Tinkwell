using System.Text.RegularExpressions;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.Ensamble;

static class EnsamblePreprocessor
{
    public static string Transform(string text)
    {
        if (text.StartsWith("// @no-preprocessor"))
            return text;

        var pattern = @"^\s*compose\s+(\w+)\s+(?:""([^""]+)""|(\w+))\s+""([^""]+)""\s*({(?:[^{}]*|{[^{}]*})*})?";
        var regex = new Regex(pattern, RegexOptions.Multiline);

        return regex.Replace(text, match =>
        {
            var kind = match.Groups[1].Value.ToLower();
            var name = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            var path = match.Groups[4].Value;
            var properties = match.Groups[5].Success ? match.Groups[5].Value : "{}";

            return Compose(kind, name, path, properties);
        });
    }

    private static string Compose(string kind, string name, string path, string properties)
    {
        try
        {
            string templatePath = Path.Combine(GetTemplatesDirectory(), $"compose_{kind}.template");
            string content = File.ReadAllText(templatePath);
            return content
                .Replace("{{ name }}", name)
                .Replace("{{ path }}", path)
                .Replace("{{ properties }}", properties)
                .Replace("{{ host.grpc }}", WellKnownNames.DefaultGrpcHostAssembly)
                .Replace("{{ host.dll }}", WellKnownNames.DefaultDllHostAssembly)
                .Replace("{{ firmlet.health_check }}", WellKnownNames.DefaaultHealthCheckService)
                .Replace("{{ address.supervisor }}", WellKnownNames.SupervisorCommandServerPipeName);
        }
        catch (FileNotFoundException e)
        {
            throw new BootstrapperException($"Template file for '{kind}' not found: {e.Message}");
        }
    }

    private static string GetTemplatesDirectory()
        => StrategyAssemblyLoader.GetEntryAssemblyDirectoryName();
}