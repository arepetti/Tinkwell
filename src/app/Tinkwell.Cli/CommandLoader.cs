using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Tinkwell.Cli.Commands;

namespace Tinkwell.Cli;

/// <summary>
/// Discovers and loads command extension DLLs at startup. Scans
/// <see cref="AppContext.BaseDirectory"/> for assemblies matching
/// <c>Tinkwell.Cli.Commands.{Domain}[.{Platform}].dll</c>.
/// Platform-specific DLLs are loaded only when the OS matches.
/// Commands are registered via <see cref="CliCommandAttribute"/>
/// and branches via <see cref="CliBranchAttribute"/>.
/// </summary>
internal static class CommandLoader
{
    private static readonly string Prefix = typeof(CliCommandAttribute).Namespace! + ".";

    private static readonly HashSet<string> PlatformNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Linux", "MacOS"
    };

    public static void RegisterExtensionCommands(IConfigurator config, ILogger logger)
    {
        var dir = AppContext.BaseDirectory;

        // Extensions are loaded into the Default ALC whose deps.json doesn't
        // list their transitive dependencies. Fall back to probing the app
        // directory so any co-located DLL can be resolved at runtime.
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(dir, name.Name + ".dll");
            return File.Exists(candidate)
                ? context.LoadFromAssemblyPath(candidate)
                : null;
        };

        foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = fileName[Prefix.Length..];
            if (remainder.Length == 0)
                continue;

            if (!ShouldLoad(remainder))
                continue;

            var fullPath = Path.GetFullPath(file);
            Assembly assembly;
            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load CLI extension {Path}", fullPath);
                continue;
            }

            RegisterCommandsFrom(assembly, config);
        }
    }

    private static bool ShouldLoad(string remainder)
    {
        var lastDot = remainder.LastIndexOf('.');

        if (lastDot < 0)
        {
            // Single segment (e.g. "Mqtt"). If it's a platform name alone, skip.
            return !PlatformNames.Contains(remainder);
        }

        var lastSegment = remainder[(lastDot + 1)..];

        if (!PlatformNames.Contains(lastSegment))
            return true;

        // Platform-specific: domain must be non-empty
        var domain = remainder[..lastDot];
        if (domain.Length == 0)
            return false;

        return IsCurrentPlatform(lastSegment);
    }

    private static bool IsCurrentPlatform(string platform) =>
        string.Equals(platform, "Windows", StringComparison.OrdinalIgnoreCase)
            ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        : string.Equals(platform, "Linux", StringComparison.OrdinalIgnoreCase)
            ? RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        : string.Equals(platform, "MacOS", StringComparison.OrdinalIgnoreCase)
            && RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static void RegisterCommandsFrom(Assembly assembly, IConfigurator config)
    {
        var branchDescs = assembly
            .GetCustomAttributes<CliBranchAttribute>()
            .ToDictionary(a => a.Name, a => a.Description, StringComparer.OrdinalIgnoreCase);

        var commandBaseType = typeof(ICommand);

        var commands = assembly.GetExportedTypes()
            .Where(t => commandBaseType.IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<CliCommandAttribute>()))
            .Where(x => x.Attr is not null)
            .ToList();

        // Root-level commands
        foreach (var (type, attr) in commands.Where(x => x.Attr!.Branch is null))
            AddCommand(config, type, attr!.Name, attr.Description);

        // Branch-level commands, grouped by branch
        foreach (var group in commands
            .Where(x => x.Attr!.Branch is not null)
            .GroupBy(x => x.Attr!.Branch!, StringComparer.OrdinalIgnoreCase))
        {
            config.AddBranch(group.Key, branch =>
            {
                if (branchDescs.TryGetValue(group.Key, out var desc))
                    branch.SetDescription(desc);

                foreach (var (type, attr) in group)
                    AddCommand(branch, type, attr!.Name, attr.Description);
            });
        }
    }

    private static void AddCommand(
        IConfigurator config, Type commandType, string name, string? description)
    {
        var builder = typeof(IConfigurator)
            .GetMethod(nameof(IConfigurator.AddCommand))!
            .MakeGenericMethod(commandType)
            .Invoke(config, [name]);

        if (description is not null)
            ((ICommandConfigurator)builder!).WithDescription(description);
    }

    private static void AddCommand(
        IConfigurator<CommandSettings> branch, Type commandType, string name, string? description)
    {
        var method = typeof(IConfigurator<CommandSettings>)
            .GetMethods()
            .First(m => m.Name == "AddCommand" && m.IsGenericMethod);

        var builder = method.MakeGenericMethod(commandType).Invoke(branch, [name]);

        if (description is not null)
            ((ICommandConfigurator)builder!).WithDescription(description);
    }
}
