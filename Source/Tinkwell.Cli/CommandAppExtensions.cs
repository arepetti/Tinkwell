using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using Spectre.Console.Cli;
using Tinkwell.Bootstrapper;

namespace Tinkwell.Cli;

static class CommandAppExtensions
{
    public static CommandApp AddCommandsViaReflection(this CommandApp app)
    {
        var allCommands = FindAllCommands().ToArray();
        app.Configure(config =>
        {
            foreach (var branchCommand in allCommands.Where(x => x.Parent is null))
            {
                config.AddBranch(branchCommand.Name, branch =>
                {
                    // Only argument and options supports DescriptionAttribute out-of-the-box, we
                    // have to add it by hand here using the attribute (and they have SetDescription()
                    // and WithDescription(), I guess the API evolved a bit over time).
                    branch.SetDescription(branchCommand.Description);
                    foreach (var subCommand in allCommands.Where(x => x.Parent == branchCommand.Type))
                    {
                        var configurator = branch
                            .AddCommand(subCommand.Name, subCommand.Type)
                            .WithDescription(subCommand.Description);

                        if (!string.IsNullOrWhiteSpace(subCommand.Alias))
                            configurator.WithAlias(subCommand.Alias);
                    }
                });
            }
        });

        return app;
    }

    private static IEnumerable<(Type Type, Type? Parent, string Name, string Description, string? Alias)> FindAllCommands()
    {
        // Some commands might have dependencies that cause errors at run-time, we load this platform-specific
        // assemblies only if we know that they're going to work.
        // TODO: we should (and I mean really really SHOULD) rework this to use StrategyAssemblyLoader properly
        var inThisAssembly = FindAllCommands(Assembly.GetExecutingAssembly());
        var extraAssemblies = StrategyAssemblyLoader.LoadAssemblies("Tinkwell.Cli", "Commands");
        var extraCommands = extraAssemblies.SelectMany(FindAllCommands);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string path = StrategyAssemblyLoader.GetEntryAssemblyDirectoryName();
            string windowsAssemblyPath = Path.Combine(path, $"{typeof(CommandAppExtensions).Namespace}.{OSPlatform.Windows}.dll");
            var assembly = Assembly.LoadFrom(windowsAssemblyPath);
            return Enumerable.Concat(inThisAssembly, FindAllCommands(assembly)).Concat(extraCommands).ToArray();
        }

        return inThisAssembly.Concat(extraCommands).ToArray();
    }

    private static IEnumerable<(Type Type, Type? Parent, string Name, string Description, string? Alias)> FindAllCommands(Assembly assembly)
    {
        var types = assembly.GetTypes().Where(IsCommand);
        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<CommandForAttribute>()!;
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            yield return (type, attribute.Parent, attribute.Name, description, attribute.Alias);
        }
    }

    private static bool IsCommand(Type type)
        => type.IsClass && typeof(ICommand).IsAssignableFrom(type) && type.GetCustomAttribute<CommandForAttribute>() is not null;

    private static ICommandConfigurator AddCommand(this IConfigurator<CommandSettings> configurator, string name, Type type)
    {
        // Unfortunately Spectre.Console.Cli exposes only AddCommand<T>() and no AddCommand(Type) but we have a Type,
        // because the whole point is to use Reflection instead of setting up all the commands by hand. Because of that
        // we need to invoke the AddCommand<T>() method via Reflection.
        var method = typeof(IConfigurator<CommandSettings>)
            .GetMethods()
            .First(m => m.Name == nameof(IConfigurator.AddCommand) && m.IsGenericMethod);

        var generic = method.MakeGenericMethod(type);
        return (ICommandConfigurator)generic.Invoke(configurator, [name])!;
    }
}