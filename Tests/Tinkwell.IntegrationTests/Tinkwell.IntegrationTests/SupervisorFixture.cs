using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Tinkwell.Bootstrapper.Ipc;
using Tinkwell.Bootstrapper.Ipc.Extensions;

namespace Tinkwell.IntegrationTests;

public class SupervisorFixture : IAsyncLifetime
{
    private Process? _supervisorProcess;
    private string? _tempCertPath;
    private const string CertPassword = "ci-certificate-password";
    private readonly IConfiguration _config;

    public X509Certificate2? ClientCertificate { get; private set; }
    public string SupervisorAppDataPath { get; private set; } = "";
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
        SupervisorAppDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(SupervisorAppDataPath);

        // 1. Create temporary directory for certificates
        _tempCertPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempCertPath);

        // 2. Generate self-signed certificate
        var twCliPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Tinkwell.Cli", "bin", "Debug", "net9.0", "Tinkwell.Cli.dll");
        var certExportPath = Path.Combine(_tempCertPath, "tinkwell-test-cert");

        var certCreationCommand = $"\"{twCliPath}\" certs create --export-path \"{_tempCertPath}\" --export-name tinkwell-test-cert --export-pem";
        await RunCommandAsync(certCreationCommand, "Creating self-signed certificate", passwordInput: CertPassword);

        // Load client certificate for gRPC client trust
        var pemCertPath = Path.Combine(_tempCertPath, "tinkwell-test-cert.pem");
        ClientCertificate = new X509Certificate2(pemCertPath);

        // 3. Set environment variables for Supervisor
        Environment.SetEnvironmentVariable("TINKWELL_CERT_PATH", Path.Combine(_tempCertPath, "tinkwell-test-cert.pfx"));
        Environment.SetEnvironmentVariable("TINKWELL_CERT_PASS", CertPassword);
        Environment.SetEnvironmentVariable("TINKWELL_APP_DATA_PATH", SupervisorAppDataPath); // Set app data path

        // 4. Start Supervisor process
        _supervisorProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project ../../../Tinkwell.Supervisor/Tinkwell.Supervisor.csproj",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = SupervisorAppDataPath,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true
        };

        _supervisorProcess.Start();

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
                    var grpcPort = 5001;
                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(ClientCertificate);
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true; // Trust all server certs for testing

                    Channel = GrpcChannel.ForAddress($"https://localhost:{grpcPort}", new GrpcChannelOptions { HttpHandler = handler });

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
        if (Channel != null)
        {
            await Channel.ShutdownAsync();
            Channel.Dispose();
        }

        if (_supervisorProcess != null && !_supervisorProcess.HasExited)
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
        if (Directory.Exists(SupervisorAppDataPath))
        {
            try
            {
                Directory.Delete(SupervisorAppDataPath, true);
                Console.WriteLine($"Cleaned up Supervisor app data directory: {SupervisorAppDataPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up Supervisor app data directory {SupervisorAppDataPath}: {ex.Message}");
            }
        }

        // Clear environment variables
        Environment.SetEnvironmentVariable("TINKWELL_CERT_PATH", null);
        Environment.SetEnvironmentVariable("TINKWELL_CERT_PASS", null);
        Environment.SetEnvironmentVariable("TINKWELL_APP_DATA_PATH", null);
    }

    private async Task RunCommandAsync(string commandAndArgs, string description, string? passwordInput = null)
    {
        Console.WriteLine($"{description}: {commandAndArgs}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = commandAndArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                //CreateNoWindow = true,
            }
        };

        process.Start();

        if (passwordInput != null)
        {
            await process.StandardInput.WriteLineAsync(passwordInput);
            await process.StandardInput.FlushAsync();
        }

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
    }
}
