using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Provides information and utilities related to the hosting environment for Tinkwell runners and services.
/// </summary>
public static class HostingInformation
{
    /// <summary>
    /// Gets the default directory (at application level) where firmlets can store data.
    /// </summary>
    public static string ApplicationDataDirectory
    {
        get
        {
            var userDefined = GetPathFromEnv(WellKnownNames.AppDataEnvironmentVariable);
            if (userDefined is not null)
                return userDefined;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tinkwell");

            if (_nixApplicationDataDirectory is null)
            {
                if (IsPathWriteable(PrefferredNixAppDataDirectory))
                    _nixApplicationDataDirectory = PrefferredNixAppDataDirectory;
                else
                    _nixApplicationDataDirectory = PrefferredNixUserDataDirectory;
            }

            return _nixApplicationDataDirectory;

        }
    }

    /// <summary>
    /// Gets the default directory (at user level) where firmlets can store data.
    /// </summary>
    public static string UserDataDirectory
    {
        get
        {
            var userDefined = GetPathFromEnv(WellKnownNames.UserDataEnvironmentVariable);
            if (userDefined is not null)
                return userDefined;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tinkwell");

            return PrefferredNixUserDataDirectory;
        }
    }

    /// <summary>
    /// Gets the application working directory.
    /// </summary>
    public static string WorkingDirectory
        => GetPathFromEnv(WellKnownNames.WorkingDirectoryEnvironmentVariable, Environment.CurrentDirectory);

    /// <summary>
    /// Get the full path of the specified path fragment.
    /// </summary>
    /// <param name="path">
    /// A path to a resource. It could be absolute or relative.
    /// </param>
    /// <returns>
    /// The full path of the specified resource. It's <paramref name="path"/> unchanged if it was
    /// a full path already. If relative then it'll be resolved referring to <see cref="WorkingDirectory"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The working directory <see cref="WorkingDirectory"/> and the current directory
    /// <see cref="Environment.CurrentDirectory"/> are usually the same but in some scenarios they may differ.
    /// Clients should always use <see cref="GetFullPath()"/> to obtain the full path of a file
    /// when it might be relative.
    /// </para>
    /// <para>
    /// <strong>Do not do</strong> this:
    /// </para>
    /// <code>
    /// string content = File.ReadAllText(path);
    /// </code>
    /// <para>
    /// <strong>Do</strong> this instead:
    /// </para>
    /// <code>
    /// string content = File.ReadAllText(HostingInformation.GetFullPath(path));
    /// </code>
    /// </remarks>
    public static string GetFullPath(string path)
    {
        if (Path.IsPathFullyQualified(path))
            return path;

        return Path.Combine(WorkingDirectory, path);
    }

    /// <summary>
    /// Gets the name of the current runner from environment variables.
    /// </summary>
    public static string RunnerName
        => Environment.GetEnvironmentVariable(WellKnownNames.RunnerNameEnvironmentVariable) ?? "";

    /// <summary>
    /// Resolves the address of the Discovery Service asynchronously.
    /// </summary>
    /// <param name="configuration">The configuration to use for resolving the address.</param>
    /// <param name="client">The named pipe client to use for communication.</param>
    /// <returns>The resolved address as a string.</returns>
    /// <exception cref="BootstrapperException">Thrown if the address cannot be resolved.</exception>
    public static async Task<string> ResolveDiscoveryServiceAddressAsync(IConfiguration configuration, INamedPipeClient client)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
        ArgumentNullException.ThrowIfNull(client, nameof(client));

        if (_discoveryServiceAddress is null)
        {
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

    /// <summary>
    /// Resolves the address of the Discovery Service asynchronously.
    /// </summary>
    /// <param name="configuration">The configuration to use for resolving the address.</param>
    /// <returns>The resolved address as a string.</returns>
    /// <exception cref="BootstrapperException">Thrown if the address cannot be resolved.</exception>
    public static async Task<string> ResolveDiscoveryServiceAddressAsync(IConfiguration configuration)
    {
        if (_discoveryServiceAddress is not null)
            return _discoveryServiceAddress;

        using var client = new NamedPipeClient();
        return await ResolveDiscoveryServiceAddressAsync(configuration, client);
    }

    /// <summary>
    /// Resolves the platform identifier for the current operating system.
    /// </summary>
    /// <returns>A string representing the platform (e.g., "windows", "linux", "osx", "bsd", "other").</returns>
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
    private const string PrefferredNixAppDataDirectory = "/var/lib/Tinkwell";
    private const string PrefferredNixUserDataDirectory = "~/.local/share/Tinkwell";

    private static string? _discoveryServiceAddress;
    private static string? _nixApplicationDataDirectory;

    [return: NotNullIfNotNull("defaultValue")]
    private static string? GetPathFromEnv(string envVariableName, string? defaultValue = null)
        => Environment.GetEnvironmentVariable(envVariableName) ?? defaultValue;

    private static bool IsPathWriteable(string path)
    {
        string tmpFilePath = Path.Combine(path, Guid.NewGuid().ToString());
        try
        {
            // We want to be sure that we can both create and delete files
            File.WriteAllText(tmpFilePath, "test");
            return TryDelete();
        }
        catch
        {
            return false;
        }

        bool TryDelete()
        {
            try
            {
                File.Delete(tmpFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static async Task<string?> QueryDiscoveryAddressAsync(IConfiguration configuration, INamedPipeClient client)
    {
        var address = await client.SendCommandToSupervisorAndDisconnectAsync(
            configuration, $"roles query \"{WellKnownNames.DiscoveryServiceRoleName}\"");

        if (address is not null && address.StartsWith("Error: "))
            return null;

        return address;
    }
}
