using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper;

public static class HostingInformation
{
    public static string RunnerName
        => Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable) ?? "";

    public static async Task<string> ResolveDiscoveryServiceAddressAsync(IConfiguration configuration, INamedPipeClient? client = default)
    {
        if (_discoveryServiceAddress is null)
        {
            client ??= new NamedPipeClient();
            for (int i=0; i < NumberOfAttemptsOnError; ++i)
            {
                _discoveryServiceAddress = await QueryDiscoveryAddressAsync(configuration, client);
                if (_discoveryServiceAddress is not null)
                    break;

                await Task.Delay(DelayBeforeRetryingOnError);
            }    

            if (_discoveryServiceAddress is null)
                throw new BootstrapperException("Cannot resolve the address for the Discovery Service host.");
        }

        return _discoveryServiceAddress!;
    }

    public static string ResolvePlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return "bsd";

        return "other";
    }

    const int NumberOfAttemptsOnError = 3;
    private const int DelayBeforeRetryingOnError = 1000;

    private static string? _discoveryServiceAddress;

    private static async Task<string?> QueryDiscoveryAddressAsync(IConfiguration configuration, INamedPipeClient client)
    {
        var address = await client.SendCommandToSupervisorAndDisconnectAsync(
            configuration, $"roles query \"{WellKnownNames.DiscoveryServiceRoleName}\"");

        if (address is not null && address.StartsWith("Error: "))
            return null;

        return address;
    }
}
