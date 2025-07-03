using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using Tinkwell.Cli;

var app = new CommandApp();
app
    .AddCommandsViaReflection()
    .Configure(config =>
    {
        config.SetApplicationName(Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location));
        config.SetExceptionHandler((exception, _) =>
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {exception.Message}");
        });
    });

await app.RunAsync(args);