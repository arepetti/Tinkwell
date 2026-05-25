using System.Diagnostics;
using Grpc.Net.Client;
using Tinkwell.Runlet.Store.Grpc.V1;

namespace Tinkwell.Integration.Tests;

/// <summary>
/// Integration test: coordinator with store (gRPC) + CoAP (headless) runners.
/// Sends a CoAP PUT to write a value into the store, then reads it back via
/// the store gRPC client to verify the full round-trip.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CoapStoreIntegrationTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tw-coap-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteConfig(string tempDir, string content)
    {
        var path = Path.Combine(tempDir, "test.tw");
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<string> WaitForStoreReadyAsync(
        CoordinatorProcess coordinator, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await coordinator.SendCommandAsync("service find store");
                if (response.GetProperty("status").GetString() == "ok" &&
                    response.TryGetProperty("data", out var data))
                {
                    var url = data.GetProperty("url").GetString();
                    if (!string.IsNullOrEmpty(url))
                        return url;
                }
            }
            catch
            {
            }

            if (coordinator.HasExited)
                break;
            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Store service did not become available within {timeout}.\n" +
            $"Coordinator output:\n{coordinator.CombinedOutput}");
    }

    private static async Task WaitForCoapReadyAsync(
        CoordinatorProcess coordinator, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (coordinator.CombinedOutput.Contains("CoAP server", StringComparison.OrdinalIgnoreCase) &&
                coordinator.CombinedOutput.Contains("listening", StringComparison.OrdinalIgnoreCase))
                return;

            if (coordinator.HasExited)
                break;
            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"CoAP server did not start within {timeout}.\n" +
            $"Coordinator output:\n{coordinator.CombinedOutput}");
    }

    private static async Task<(string Output, int ExitCode)> RunTwAsync(
        string workingDir, CancellationToken ct, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("tw.dll");
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (stdout + stderr, process.ExitCode);
    }

    [Fact]
    public async Task Coap_Put_Then_Store_Get_RoundTrip()
    {
        var tempDir = CreateTempDir();
        try
        {
        var configPath = WriteConfig(tempDir, """
            runner grpc-store from "Tinkwell.Runner.Grpc.dll" {
                runlet store from "Tinkwell.Runlet.Store.dll" {
                    storage = "memory"
                }
            }

            runner headless-coap from "Tinkwell.Runner.Headless.dll" {
                runlet coap from "Tinkwell.Runlet.Coap.dll";
            }

            coap store-server {
                port = 5683
                resource "/store/+" {
                    on put {
                        bind store from "Tinkwell.Integrations" {
                            bucket = "default"
                            key = (segment(path, -1))
                        }
                    }
                }
            }
            """);

        var pipeName = CoordinatorProcess.UniquePipeName();
        await using var coordinator = CoordinatorProcess.Start(
            configPath,
            pipeName,
            "--Coordinator:ReadyTimeoutSeconds=30");

        var storeUrl = await WaitForStoreReadyAsync(coordinator, TimeSpan.FromSeconds(30));
        await WaitForCoapReadyAsync(coordinator, TimeSpan.FromSeconds(30));

        using var channel = GrpcChannel.ForAddress(storeUrl);
        var storeClient = new StateStore.StateStoreClient(channel);

        const string key = "coap-test-key";
        const string payload = """{"temp":21.5}""";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var (coapOutput, coapExit) = await RunTwAsync(
            TestPaths.ArtifactsDir, cts.Token,
            "coap", "send", "put", $"/store/{key}",
            "--payload", payload,
            "--host", "127.0.0.1", "--port", "5683", "--timeout", "10");

        await Task.Delay(500);

        var logs = coordinator.CombinedOutput;

        Assert.True(
            coapOutput.Contains("2.04") || coapOutput.Contains("2.05"),
            $"Expected CoAP 2.04/2.05 but got:\n{coapOutput}\n\n--- Coordinator + runner logs ---\n{logs}");

        var response = await storeClient.GetAsync(new GetRequest
        {
            BucketId = "default",
            Key = key,
        });

        Assert.Equal(payload, response.Value);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch
            {
            }
        }
    }
}
