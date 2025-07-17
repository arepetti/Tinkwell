using Grpc.Net.Client;
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

    private Process? _supervisorProcess;
    private string? _tempCertPath;
    private readonly IConfiguration _config;

    public X509Certificate2? ClientCertificate { get; private set; }
    public string SupervisorWorkingDirectory { get; private set; } = "";
    public GrpcChannel? Channel { get; private set; }
    
    public SupervisorFixture()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Supervisor:CommandServer:ServerName", "." },
                { "Supervisor:CommandServer:PipeName", WellKnownNames.SupervisorCommandServerPipeName },
            })
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Create a unique temporary directory for Supervisor's app data
        SupervisorWorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(SupervisorWorkingDirectory);

        // 1. Create temporary directory for certificates
        _tempCertPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempCertPath);

        // 2. Generate self-signed certificate
        await CreateSelfSignedCertificate();

        // Load client certificate for gRPC client trust
        var pemCertPath = Path.Combine(_tempCertPath, "tinkwell-test-cert.pem");
        ClientCertificate = new X509Certificate2(pemCertPath);

        // 3. Set environment variables for Supervisor
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePath, Path.Combine(_tempCertPath, "tinkwell-test-cert.pfx"));
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePass, CertPassword);
        Environment.SetEnvironmentVariable(WellKnownNames.WorkingDirectoryEnvironmentVariable, SupervisorWorkingDirectory); // Set app data path

        // 4. Start Supervisor process
        _supervisorProcess = RunCommand($"run --project \"{SupervisorProjectPath}");

        // 5. Wait for Supervisor to be ready using ping command
        var client = new NamedPipeClient();
        var maxAttempts = 60; // Wait up to 60 seconds
        var delay = TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var response = await client.SendCommandToSupervisorAndDisconnectAsync(_config, "ping");
                if (response?.Trim()?.Equals("OK", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    Console.WriteLine("Supervisor is ready.");

                    // Discover Discovery Service address
                    var discoveryAddress = await client.SendCommandToSupervisorAndDisconnectAsync(_config, "roles query tinkwell-discovery-service");
                    if (string.IsNullOrWhiteSpace(discoveryAddress))
                        throw new InvalidOperationException("Could not discover Tinkwell.Discovery service address.");

                    Console.WriteLine($"Discovery Service Address: {discoveryAddress}");

                    // We should have this in Tinkwell.Services.Proto so that all the clients
                    // automatically use this certificate. The point is how to pass them this
                    // information without making tests a special case in the code.
                    //var handler = new HttpClientHandler();
                    //handler.ClientCertificates.Add(ClientCertificate);
                    //handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                    //Channel = GrpcChannel.ForAddress(discoveryAddress, new GrpcChannelOptions { HttpHandler = handler });

                    return;
                }
                Console.WriteLine($"Supervisor not ready yet: {response?.Trim()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attempt {i + 1} to connect to Supervisor failed: {ex.Message}");
            }
            await Task.Delay(delay);
        }

        throw new TimeoutException("Supervisor did not become ready within the expected time.");
    }

    public async Task DisposeAsync()
    {
        if (Channel is not null)
        {
            await Channel.ShutdownAsync();
            Channel.Dispose();
        }

        if (_supervisorProcess is not null && !_supervisorProcess.HasExited)
        {
            // Send shutdown command
            try
            {
                var client = new NamedPipeClient();
                await client.SendCommandToSupervisorAndDisconnectAsync(_config, "shutdown");
                Console.WriteLine("Shutdown command sent to Supervisor.");

                if (!_supervisorProcess.WaitForExit(TimeSpan.FromSeconds(30))) // Wait up to 30 seconds for graceful exit
                {
                    Console.WriteLine("Supervisor did not exit gracefully, forcing termination.");
                    _supervisorProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Supervisor shutdown: {ex.Message}");
                if (!_supervisorProcess.HasExited)
                {
                    _supervisorProcess.Kill(); // Ensure it's terminated even if shutdown command failed
                }
            }
        }

        // Clean up temporary certificate files
        if (Directory.Exists(_tempCertPath))
        {
            try
            {
                Directory.Delete(_tempCertPath, true);
                Console.WriteLine($"Cleaned up temporary certificate directory: {_tempCertPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up temporary directory {_tempCertPath}: {ex.Message}");
            }
        }

        // Clean up Supervisor app data directory
        if (Directory.Exists(SupervisorWorkingDirectory))
        {
            try
            {
                Directory.Delete(SupervisorWorkingDirectory, true);
                Console.WriteLine($"Cleaned up Supervisor app data directory: {SupervisorWorkingDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up Supervisor app data directory {SupervisorWorkingDirectory}: {ex.Message}");
            }
        }

        // Clear environment variables
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePath, null);
        Environment.SetEnvironmentVariable(WellKnownNames.WebServerCertificatePass, null);
        Environment.SetEnvironmentVariable(WellKnownNames.WorkingDirectoryEnvironmentVariable, null);
    }

    private async Task CreateSelfSignedCertificate()
    {
        var command = $"run --project \"{CliProjectPath} certs create --export-path \"{_tempCertPath}\" --export-name tinkwell-test-cert --export-pem";
        await RunCommandAndWaitForExitAsync(command, async stdin =>
        {
            await stdin.WriteLineAsync(CertPassword);
            await stdin.FlushAsync();
        });
    }

    private Process RunCommand(string commandAndArgs)
    {
        Console.WriteLine($"Executing dotnet {commandAndArgs}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = commandAndArgs,
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

    private async Task<string> RunCommandAndWaitForExitAsync(string commandAndArgs, Func<StreamWriter, Task>? whenReady = null)
    {
        var process = RunCommand(commandAndArgs);

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
