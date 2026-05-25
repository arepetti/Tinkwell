using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Runlet.Lwm2m.Configuration;

/// <summary>
/// Parses LwM2M server and object-mapping definitions from <c>.tw</c> files.
/// Collects top-level <c>lwm2m</c> blocks.
///
/// Expected syntax:
/// <code>
/// lwm2m my-server {
///     port = 5683
///
///     registration {
///         default-lifetime = 86400
///         emit-events = true
///     }
///
///     object 3303 {
///         resource 5700 {
///             measure = "temperature"
///             observable = true
///         }
///     }
/// }
/// </code>
/// </summary>
public sealed class Lwm2mConfigParser : ConfigurationParser<Lwm2mConfig>
{
    public Lwm2mConfigParser(ILogger? logger = null, ParserOptions? options = null)
        : base(logger, options ?? new ParserOptions { Lax = true })
    {
    }

    protected override ValueTask<Lwm2mConfig> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var servers = new List<Lwm2mServerDefinition>();

        foreach (var block in document.Blocks)
        {
            if (!string.Equals(block.Type, "lwm2m", StringComparison.Ordinal))
                continue;
            servers.Add(ParseServer(block, names));
        }

        return ValueTask.FromResult(new Lwm2mConfig(servers));
    }

    private static Lwm2mServerDefinition ParseServer(ConfigBlock block, HashSet<string> names)
    {
        if (!names.Add(block.Name))
        {
            throw new ConfigurationSyntaxException(
                $"Duplicate lwm2m server name '{block.Name}'.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        int port = 5683;
        var registration = new Lwm2mRegistrationOptions();
        var objects = new List<Lwm2mObjectMapping>();

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "port":
                    port = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown property '{prop.Key}' on lwm2m server '{block.Name}'.",
                        prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
            }
        }

        foreach (var child in block.Children)
        {
            switch (child.Type)
            {
                case "registration":
                    registration = ParseRegistration(child, block.Name);
                    break;
                case "object":
                    ParseObject(child, block.Name, objects);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unexpected block type '{child.Type}' in lwm2m '{block.Name}'. " +
                        "Only 'registration' and 'object' blocks are allowed.",
                        child.Location.FilePath, child.Location.Line, child.Location.Column);
            }
        }

        return new Lwm2mServerDefinition(block.Name, port, objects, registration, block.Location);
    }

    private static Lwm2mRegistrationOptions ParseRegistration(ConfigBlock block, string serverName)
    {
        int defaultLifetime = 86400;
        bool emitEvents = true;

        foreach (var prop in block.Properties)
        {
            switch (prop.Key)
            {
                case "default-lifetime":
                    defaultLifetime = ConfigValueConverter.ConvertTo<int>(prop.Value, prop.Location);
                    break;
                case "emit-events":
                    emitEvents = ConfigValueConverter.ConvertTo<bool>(prop.Value, prop.Location);
                    break;
                default:
                    throw new ConfigurationSyntaxException(
                        $"Unknown property '{prop.Key}' in registration block of lwm2m '{serverName}'.",
                        prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
            }
        }

        return new Lwm2mRegistrationOptions
        {
            DefaultLifetimeSeconds = defaultLifetime,
            EmitEvents = emitEvents,
        };
    }

    private static void ParseObject(
        ConfigBlock block, string serverName, List<Lwm2mObjectMapping> mappings)
    {
        if (!int.TryParse(block.Name, out var objectId))
        {
            throw new ConfigurationSyntaxException(
                $"Object block name '{block.Name}' in lwm2m '{serverName}' must be a numeric object ID.",
                block.Location.FilePath, block.Location.Line, block.Location.Column);
        }

        foreach (var child in block.Children)
        {
            if (!string.Equals(child.Type, "resource", StringComparison.Ordinal))
            {
                throw new ConfigurationSyntaxException(
                    $"Unexpected block type '{child.Type}' in object {objectId}. " +
                    "Only 'resource' blocks are allowed.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }

            if (!int.TryParse(child.Name, out var resourceId))
            {
                throw new ConfigurationSyntaxException(
                    $"Resource block name '{child.Name}' in object {objectId} must be a numeric resource ID.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }

            string? measureName = null;
            bool observable = false;

            foreach (var prop in child.Properties)
            {
                switch (prop.Key)
                {
                    case "measure":
                        measureName = ConfigValueConverter.ConvertTo<string>(prop.Value, prop.Location);
                        break;
                    case "observable":
                        observable = ConfigValueConverter.ConvertTo<bool>(prop.Value, prop.Location);
                        break;
                    default:
                        throw new ConfigurationSyntaxException(
                            $"Unknown property '{prop.Key}' in resource {resourceId} of object {objectId}.",
                            prop.Location.FilePath, prop.Location.Line, prop.Location.Column);
                }
            }

            if (measureName is null)
            {
                throw new ConfigurationSyntaxException(
                    $"Resource {resourceId} in object {objectId} is missing required 'measure' property.",
                    child.Location.FilePath, child.Location.Line, child.Location.Column);
            }

            mappings.Add(new Lwm2mObjectMapping(
                objectId, resourceId, measureName, observable, child.Location));
        }
    }
}
