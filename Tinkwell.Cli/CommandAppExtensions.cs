using System.ComponentModel;
using System.Data;
using System.Reflection;
using Spectre.Console.Cli;

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
                    branch.SetDescription(branchCommand.Description);
                    foreach (var subCommand in allCommands.Where(x => x.Parent == branchCommand.Type))
                    {
                        branch
                            .AddCommand(subCommand.Name, subCommand.Type)
                            .WithDescription(subCommand.Description);
                    }
                });
            }
        });

        return app;
    }

    private static IEnumerable<(Type Type, Type? Parent, string Name, string Description)> FindAllCommands()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(IsCommand);
        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<CommandForAttribute>()!;
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            yield return (type, attribute.Parent, attribute.Name, description);
        }
    }

    private static bool IsCommand(Type type)
        => type.IsClass && typeof(ICommand).IsAssignableFrom(type) && type.GetCustomAttribute<CommandForAttribute>() is not null;

    private static ICommandConfigurator AddCommand(this IConfigurator<CommandSettings> configurator, string name, Type type)
    {
        var method = typeof(IConfigurator<CommandSettings>)
            .GetMethods()
            .First(m => m.Name == nameof(IConfigurator.AddCommand) && m.IsGenericMethod);

        var generic = method.MakeGenericMethod(type);
        return (ICommandConfigurator)generic.Invoke(configurator, [name])!;
    }
}