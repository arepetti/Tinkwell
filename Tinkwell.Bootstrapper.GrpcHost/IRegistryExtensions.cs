using System.Text;

namespace Tinkwell.Bootstrapper.GrpcHost;

static class IRegistryExtensions
{
    public static string AsText(this IRegistry registry)
    {
        var text = new StringBuilder();
        foreach (var service in registry.Services)
        {
            string? name = string.IsNullOrWhiteSpace(service.FriendlyName) ? service.Name : service.FriendlyName;
            if (!string.IsNullOrWhiteSpace(service.FamilyName) && !string.Equals(name, service.FamilyName, StringComparison.OrdinalIgnoreCase))
                name = $"{name} ({service.FamilyName})";
            text.AppendLine($"{name}={service.Url}");
        }
        return text.ToString();
    }
}
