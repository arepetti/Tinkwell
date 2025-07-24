using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Net.Sockets;
using Tinkwell;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;
using Tinkwell.Bootstrapper.WebHost;

var settings = await WebHostOptions.LoadAsync();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = settings.WebRootPath,
    ContentRootPath = settings.WebRootPath,
});

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
    options.IncludeScopes = false;
});

builder.WebHost.ConfigureKestrel(options =>
{
    var hostName = Dns.GetHostName();
    var ipAddresses = Dns.GetHostEntry(hostName).AddressList;

    foreach (var ipAddress in ipAddresses)
    {
        if (ipAddress.AddressFamily == AddressFamily.InterNetwork ||
            ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            options.Listen(ipAddress, settings.PortNumber, configure =>
            {
                configure.Protocols = HttpProtocols.Http2;
                if (!string.IsNullOrWhiteSpace(settings.CertificatePath))
                    configure.UseHttps(settings.CertificatePath, settings.CertificatePassword);
            });
        }
    }
});

var app = builder.Build();
app.UseHttpsRedirection()
   .UseDefaultFiles()
   .UseStaticFiles();

app.MapGet("/api/v1/services", async (string? name) =>
{
    if (string.IsNullOrEmpty(name))
        return Results.BadRequest("Service name is required.");

    var locator = new ServiceLocator(app.Configuration, new NamedPipeClient());
    var address = await locator.FindServiceAddressAsync(name);
    if (string.IsNullOrWhiteSpace(address))
        return Results.NotFound();

    return Results.Ok(address);
});

using var watcher = ParentProcessWatcher.WhenNotFound(() =>
{
    app.Logger.LogWarning("Parent process has been terminated, quitting.");
    app.Lifetime.StopApplication();
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var client = new NamedPipeClient();
    client.SendCommandToSupervisorAndDisconnectAsync(app.Configuration, $"signal \"{HostingInformation.RunnerName}\"")
        .GetAwaiter()
        .GetResult();
});

app.Run();
