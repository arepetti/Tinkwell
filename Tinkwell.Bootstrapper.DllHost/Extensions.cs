using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Bootstrapper.DllHost;

static class Extensions
{
    public static string RunnerName
        => Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable) ?? "";

    public static async Task DelegateConfigureServicesAsync(this IHostBuilder builder, string[] args)
    {
        // We use the shadow host to support DI when configuring
        // child libraries because we can't add services after we called Build() and we
        // cannot call build twice (but we need DI before calling Build()!)
       using  var shadowHost = Host.CreateDefaultBuilder(args)
            .AddWorker()
            .Build();

        var activity = shadowHost.Services.GetRequiredService<IActivity>();
        await activity.ConfigureBuilderAsync(builder, CancellationToken.None);
    }
}
