using System.Text;

namespace Tinkwell.Bootstrapper.GrpcHost;

static class IRegistryExtensions
{
    public static string AsText(this IRegistry registry)
    {
        var text = new StringBuilder();
        foreach (var service in registry.Services)
        {
            string[] columns =
            [
                service.Name ?? "?",
                service.FamilyName ?? "",
                service.Host ?? "",
                service.Url ?? ""
            ];

            text.AppendLine(string.Join(',', columns.Select(x => $"\"{x}\"")));
        }
        return text.ToString();
    }
}
