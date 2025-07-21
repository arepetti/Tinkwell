using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;
using Tinkwell.Cli.Commands;

var app = new CommandApp();
app
    .AddCommandsViaReflection()
    .Configure(config =>
    {
        config.SetApplicationName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location));
        config.SetExceptionHandler((exception, _) =>
        {
            Consoles.Error.MarkupLineInterpolated($"[red]Error:[/] {exception.Message}");
        });
    });


app.SetDefaultCommand<RootCommand>();

await app.RunAsync(args);