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
            var block = match.Groups[5].Success ? match.Groups[5].Value : "{}";

            var host = kind switch
            {
                "service" => WellKnownNames.DefaultGrpcHostAssembly,
                "agent" => WellKnownNames.DefaultDllHostAssembly,
                _ => throw new NotSupportedException($"Unsupported host type: {kind}")
            };

            if (kind == "service")
            {
                return string.Format("\nrunner \"{0}\" \"{1}\" {{\n service runner \"_{0}___firmlet_\" \"{2}\" {{\n  properties {3}\n }}\n service runner \"_{0}___healthcheck_\" \"{4}\" {{}}\n}}\n",
                    name, host, path, block, WellKnownNames.DefaaultHealthCheckService);
            }
           
            return string.Format("\nrunner \"{0}\" \"{1}\" {{\n service runner \"_{0}___firmlet_\" \"{2}\" {{\n  properties {3}\n }}\n}}\n",
                name, host, path, block);
        });
    }
}