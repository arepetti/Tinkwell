using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Tinkwell.Cli;

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole());
var logger = loggerFactory.CreateLogger("Tinkwell.Cli.CommandLoader");
var app = new CommandApp();
app.Configure(c => AppConfigurator.Configure(c, logger));
return await app.RunAsync(args);
