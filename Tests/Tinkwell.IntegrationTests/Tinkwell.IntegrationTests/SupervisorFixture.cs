using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.IntegrationTests;

public class SupervisorFixture : IAsyncLifetime
{
    private const string SupervisorProjectPath = "../../../Tinkwell.Supervisor/Tinkwell.Supervisor.csproj";
    private const string CliProjectPath = "../../../Tinkwell.Cli/Tinkwell.Cli.csproj";
    private const string CertPassword = "ci-certificate-password";
    private const string SupervisorPipeName = "tinkwell-isolated-command-pipe";

    private const int MaximumNumberOfAttempts = 60;
    private const int DelayBetweenAttemptsInSeconds = 1;
    private const int MaximumWaitingToShutdownInSeconds = 30;

    public X509Certificate2? ClientCertificate { get; private set; }
    public string SupervisorWorkingDirectory { get; private set; } = "";
    
    public SupervisorFixture()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supervisor:CommandServer:ServerName", "." },
                { "Supervisor:CommandServer:PipeName", SupervisorPipeName },
            })
            .Build();
    }

    public async Task InitializeAsync()
    {
        await CreateIsolatedEnvironment();
        string? discoveryAddress = await StartSupervisorAsync();
        if (string.IsNullOrWhiteSpace(discoveryAddress))
            throw new InvalidOperationException("Cannot obtain the Discovery Service address.");

        Console.WriteLine($"Discovery Service address: {discoveryAddress}");
        // We should have this in Tinkwell.Services.Proto so that all the clients
        // automatically use this certificate. The point is how to pass them this
        // information without making tests a special case in the code.
        //var handler = new HttpClientHandler();
        //handler.ClientCertificates.Add(ClientCertificate);
        //handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        //Channel = GrpcChannel.ForAddress(discoveryAddress, new GrpcChannelOptions { HttpHandler = handler });
    }

    public async Task DisposeAsync()
    {
        await ShutdownSupervisorAsync();
        RemoveTemporaryFiles();

        // Clear environment variables
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePath, null);
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePass, null);
        Environment.SetEnvironmentVariable(WellKnownNames.WorkingDirectoryEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(WellKnownNames.AppDataEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(WellKnownNames.UserDataEnvironmentVariable, null);
    }

    private Process? _supervisorProcess;
    private string? _temporaryFolder;
    private string? _certPath;
    private readonly IConfiguration _config;

    private async Task CreateIsolatedEnvironment()
    {
        // Create temporary directories, we do not want the app to leave traces in the system!
        SupervisorWorkingDirectory = GetTempPath("Root", WellKnownNames.WorkingDirectoryEnvironmentVariable);
        GetTempPath("AppData", WellKnownNames.AppDataEnvironmentVariable);
        GetTempPath("UserData", WellKnownNames.UserDataEnvironmentVariable);

        // Generate a self-signed certificate valid only for this session
        _certPath = GetTempPath("Certs", null);
        await CreateSelfSignedCertificate();

        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePath, Path.Combine(_certPath, "tinkwell-test-cert.pfx"));
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePass, CertPassword);

        var pemCertPath = Path.Combine(_certPath, "tinkwell-test-cert.pem");
        ClientCertificate = new X509Certificate2(pemCertPath);
    }

    private async Task<string?> StartSupervisorAsync()
    {
        _supervisorProcess = RunProject(SupervisorProjectPath, $"--Supervisor:CommandServer:PipeName={SupervisorPipeName}");

        using var client = new NamedPipeClient();
        await TryAsync("Start the Supervisor", async () =>
        {
            var response = await client.SendCommandToSupervisorAndDisconnectAsync(_config, "ping");
            return response?.Trim()?.Equals("OK", StringComparison.OrdinalIgnoreCase) ?? false;
        });

        return await client.SendCommandToSupervisorAndDisconnectAsync(_config, $"roles query {WellKnownNames.DiscoveryServiceRoleName}");
    }

    private async Task ShutdownSupervisorAsync()
    {
        if (_supervisorProcess is null || _supervisorProcess.HasExited)
            return;

        // We try to shutdown gracefully sending the "shutdown" command but if it takes
        // too long then we go brute force and kill the process (all its child processes
        // should follow because they should use the ParentProcessMonitor);
        try
        {
            var client = new NamedPipeClient();
            await client.SendCommandToSupervisorAndDisconnectAsync(_config, "shutdown");
            Console.WriteLine("Shutdown command sent to Supervisor, waiting for exit.");

            if (!_supervisorProcess.WaitForExit(TimeSpan.FromSeconds(MaximumWaitingToShutdownInSeconds)))
            {
                Console.WriteLine("Supervisor did not exit gracefully, forcing termination and waiting for child processes to terminate.");
                _supervisorProcess.Kill();
                await Task.Delay(TimeSpan.FromSeconds(MaximumWaitingToShutdownInSeconds));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during Supervisor shutdown: {ex.Message}");
            if (!_supervisorProcess.HasExited)
                _supervisorProcess.Kill();
        }
    }

    private void RemoveTemporaryFiles()
    {
        if (string.IsNullOrEmpty(_temporaryFolder) || !Directory.Exists(_temporaryFolder))
            return;

        try
        {
            Directory.Delete(_temporaryFolder, true);
            Console.WriteLine($"Removed temporary directory: {_temporaryFolder}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error cleaning up temporary directory {_temporaryFolder}: {e.Message}");
        }
    }

    private async Task CreateSelfSignedCertificate()
    {
        var command = $"certs create --export-path \"{_certPath}\" --export-name tinkwell-test-cert --export-pem";
        await RunProjectAndWaitForExitAsync(CliProjectPath, command, async stdin =>
        {
            await stdin.WriteLineAsync(CertPassword);
            await stdin.FlushAsync();
        });
    }

    private string GetTempPath(string name, string? environmentVariable)
    {
        if (_temporaryFolder is null)
        {
            _temporaryFolder = Path.Combine(Path.GetTempPath(), $"Tinkwell.{Guid.NewGuid()}");
            Directory.CreateDirectory(_temporaryFolder);
        }

        string path = Path.Combine(_temporaryFolder, name);
        Directory.CreateDirectory(path);

        if (environmentVariable is not null)
            Environment.SetEnvironmentVariable(environmentVariable, path);

        return path;
    }

    private async Task TryAsync(string description, Func<Task<bool>> action)
    {
        Console.WriteLine($"Attempting to '{description}'...");
        for (int i = 0; i < MaximumNumberOfAttempts; ++i)
        {
            try
            {
                if (await action())
                {
                    Console.WriteLine($"Task '{description}' completed successfully.");
                    return;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"Attempt {i + 1} of {MaximumNumberOfAttempts} to '{description}' failed: {e.Message}");
            }

            await Task.Delay(DelayBetweenAttemptsInSeconds * 1000);
        }

        Console.WriteLine($"Task '{description}' timed out.");
        throw new TimeoutException($"Task '{description}' cannot be completed in the expected time.");
    }

    private Process RunProject(string projectPath, string commandAndArgs)
    {
        Console.WriteLine($"Executing project \"{projectPath}\" with {commandAndArgs}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" {commandAndArgs}",
                WorkingDirectory = SupervisorWorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        return process;
    }

    private async Task<string> RunProjectAndWaitForExitAsync(string projectPath, string commandAndArgs, Func<StreamWriter, Task>? whenReady = null)
    {
        var process = RunProject(projectPath, commandAndArgs);

        if (whenReady is not null)
            await whenReady(process.StandardInput);

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Command failed with exit code {process.ExitCode}");
            Console.WriteLine($"Output: {output}");
            Console.WriteLine($"Error: {error}");
            throw new InvalidOperationException($"Command failed: {commandAndArgs}");
        }
        Console.WriteLine($"Command output: {output}");
        return output;
    }
}
