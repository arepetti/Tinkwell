using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tinkwell.Coordinator.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator;
using Tinkwell.Coordinator.Pipes;
using Tinkwell.Runner;

namespace Tinkwell.Coordinator.Tests;

public class PipeCommandDispatcherTests
{
    private static RunnerConfig MakeRunnerConfig(string name = "test-runner", params RunletConfig[] runlets) =>
        MakeRunnerConfigWithOptions(name, new Dictionary<string, ConfigValue>(), runlets);

    private static RunnerConfig MakeRunnerConfigWithOptions(
        string name, Dictionary<string, ConfigValue> options, params RunletConfig[] runlets) =>
        new(name, $"runners/{name}", options, runlets, new SourceLocation("test.tw", 1, 1));

    private static RunletConfig MakeRunletConfig(string name, params (string Key, ConfigValue Value)[] options) =>
        new(name, $"runlets/{name}.dll",
            options.ToDictionary(o => o.Key, o => o.Value),
            new SourceLocation("test.tw", 1, 1));

    private static EnsembleConfig MakeEnsemble(params RunnerConfig[] runners) =>
        new(runners);

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopApplicationCalled { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopApplicationCalled = true;
    }

    private static readonly JsonSerializerOptions ServiceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static (PipeCommandDispatcher dispatcher, RunnerRegistry registry, TestHostApplicationLifetime lifetime) CreateDispatcher(
        params RunnerConfig[] runners)
    {
        var config = MakeEnsemble(runners);
        var registry = new RunnerRegistry(config);
        var lifetime = new TestHostApplicationLifetime();
        const string testConfigPath = @"C:\coord\pipe-dispatcher-tests.tw";

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton(new ServiceRegistry(registry));
        services.AddSingleton(new ConfigPathInfo(testConfigPath));
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddSingleton(Options.Create(new EndpointOptions()));
        services.AddSingleton<EndpointAllocator>();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        var provider = services.BuildServiceProvider();

        var dispatcher = new PipeCommandDispatcher(
            provider, NullLogger<PipeCommandDispatcher>.Instance);
        return (dispatcher, registry, lifetime);
    }

    private record PipeResponse(string Status, string? Message, JsonElement? Data);

    private static PipeResponse ParseResponse(string json) =>
        JsonSerializer.Deserialize<PipeResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;

    private static void AssertOk(string response)
    {
        var r = ParseResponse(response);
        Assert.Equal("ok", r.Status);
    }

    private static void AssertError(string response, string? containsMessage = null)
    {
        var r = ParseResponse(response);
        Assert.Equal("error", r.Status);
        Assert.NotNull(r.Message);
        if (containsMessage is not null)
            Assert.Contains(containsMessage, r.Message);
    }

    private static string BuildServiceRegisterCommand(string runnerId, IReadOnlyList<ServiceDefinition> services)
    {
        var json = JsonSerializer.Serialize(services, ServiceJsonOptions);
        var escaped = json.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"service register {runnerId} \"{escaped}\"";
    }

    [Fact]
    public async Task EmptyCommand_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("");
        AssertError(result, "empty command");
    }

    [Fact]
    public async Task UnknownCommand_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("foo bar");
        AssertError(result);
    }

    [Fact]
    public async Task NotifyReady_ValidId_ReturnsOkAndSetsStatus()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];
        runner.MarkWaitingForReady();

        var result = await dispatcher.DispatchAsync($"notify ready {runner.Id}");

        AssertOk(result);
        Assert.Equal(RunnerStatus.Ready, runner.Status);
    }

    [Fact]
    public async Task NotifyReady_InvalidGuid_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify ready not-a-guid");
        AssertError(result);
    }

    [Fact]
    public async Task NotifyReady_UnknownId_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify ready 00000000");
        AssertError(result, "unknown");
    }

    [Fact]
    public async Task NotifyReady_MissingId_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify ready");
        AssertError(result);
    }

    [Fact]
    public async Task NotifyFatal_ValidId_ReturnsOkAndSetsStatus()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];
        runner.MarkWaitingForReady();

        var result = await dispatcher.DispatchAsync(
            $"notify fatal {runner.Id} \"runlet mismatch\"");

        AssertOk(result);
        Assert.Equal(RunnerStatus.Fatal, runner.Status);
        Assert.Equal("runlet mismatch", runner.FatalMessage);
    }

    [Fact]
    public async Task NotifyFatal_NoMessage_UsesDefault()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync($"notify fatal {runner.Id}");

        AssertOk(result);
        Assert.Equal(RunnerStatus.Fatal, runner.Status);
        Assert.Equal("no details provided", runner.FatalMessage);
    }

    [Fact]
    public async Task NotifyFatal_InvalidGuid_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify fatal bad-id \"some message\"");
        AssertError(result);
    }

    [Fact]
    public async Task NotifyUnblock_ReturnsOk()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify unblock");
        AssertOk(result);
    }

    [Fact]
    public async Task NotifyUnblock_UnblocksWaitingRunners()
    {
        var (dispatcher, registry, _) = CreateDispatcher(
            MakeRunnerConfig("a"), MakeRunnerConfig("b"));

        var runners = registry.All;
        runners[0].MarkWaitingForReady();
        runners[1].MarkReady();

        await dispatcher.DispatchAsync("notify unblock");

        Assert.Equal(RunnerStatus.Unblocked, runners[0].Status);
        Assert.Equal(RunnerStatus.Ready, runners[1].Status);
    }

    [Fact]
    public async Task NotifySubcommand_Missing_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify");
        AssertError(result);
    }

    [Fact]
    public async Task NotifyReady_SignalsWaitingRunner()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];
        runner.MarkWaitingForReady();

        var waitTask = runner.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await dispatcher.DispatchAsync($"notify ready {runner.Id}");

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Ready, result);
    }

    [Fact]
    public async Task NotifyFatal_SignalsWaitingRunner()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];
        runner.MarkWaitingForReady();

        var waitTask = runner.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await dispatcher.DispatchAsync($"notify fatal {runner.Id} \"crash boom\"");

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Fatal, result);
    }

    [Fact]
    public async Task NotifyUnblock_SignalsWaitingRunner()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];
        runner.MarkWaitingForReady();

        var waitTask = runner.WaitForReadyAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await dispatcher.DispatchAsync("notify unblock");

        var result = await waitTask;
        Assert.Equal(ReadySignalResult.Unblocked, result);
    }

    [Fact]
    public async Task ResponseEnvelope_IsValidJson()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("notify unblock");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ErrorEnvelope_ContainsMessageField()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task ConfigRead_ValidId_ReturnsRunletDescriptors()
    {
        var runlet = MakeRunletConfig("my-runlet",
            ("key1", new StringValue("val1")),
            ("key2", new LongValue(42)));
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a", runlet));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync($"config read {runner.Id}");
        var response = ParseResponse(result);

        Assert.Equal("ok", response.Status);
        Assert.NotNull(response.Data);

        var data = response.Data.Value;
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.Equal("runner-a", data.GetProperty("name").GetString());

        var runlets = data.GetProperty("runlets");
        Assert.Equal(1, runlets.GetArrayLength());

        var first = runlets[0];
        Assert.Equal("my-runlet", first.GetProperty("name").GetString());
        Assert.Equal("runlets/my-runlet.dll", first.GetProperty("assemblyPath").GetString());
        Assert.Equal("val1", first.GetProperty("settings").GetProperty("key1").GetString());
        Assert.Equal("42", first.GetProperty("settings").GetProperty("key2").GetString());
    }

    [Fact]
    public async Task ConfigRead_NoRunlets_ReturnsEmptyArray()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync($"config read {runner.Id}");
        var response = ParseResponse(result);

        Assert.Equal("ok", response.Status);
        var data = response.Data!.Value;
        Assert.Equal("runner-a", data.GetProperty("name").GetString());
        Assert.Equal(0, data.GetProperty("runlets").GetArrayLength());
    }

    [Fact]
    public async Task ConfigRead_IncludesRunnerSettings()
    {
        var options = new Dictionary<string, ConfigValue>
        {
            ["port"] = new LongValue(8080),
            ["label"] = new StringValue("primary")
        };
        var (dispatcher, registry, _) = CreateDispatcher(
            MakeRunnerConfigWithOptions("runner-a", options));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync($"config read {runner.Id}");
        var data = ParseResponse(result).Data!.Value;

        var settings = data.GetProperty("settings");
        Assert.Equal("8080", settings.GetProperty("port").GetString());
        Assert.Equal("primary", settings.GetProperty("label").GetString());
    }

    [Fact]
    public async Task ConfigRead_UnknownId_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("config read 00000000");
        AssertError(result, "unknown");
    }

    [Fact]
    public async Task ConfigRead_InvalidGuid_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("config read not-a-guid");
        AssertError(result);
    }

    [Fact]
    public async Task ConfigRead_FlattensAllValueTypes()
    {
        var runlet = MakeRunletConfig("r",
            ("str", new StringValue("hello")),
            ("lng", new LongValue(99)),
            ("dbl", new DoubleValue(3.14)),
            ("bln", BoolValue.True),
            ("expr", new ExpressionValue("x + 1")));
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner", runlet));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync($"config read {runner.Id}");
        var settings = ParseResponse(result).Data!.Value.GetProperty("runlets")[0].GetProperty("settings");

        Assert.Equal("hello", settings.GetProperty("str").GetString());
        Assert.Equal("99", settings.GetProperty("lng").GetString());
        Assert.Equal("3.14", settings.GetProperty("dbl").GetString());
        Assert.Equal("true", settings.GetProperty("bln").GetString());
        Assert.Equal("x + 1", settings.GetProperty("expr").GetString());
    }

    [Fact]
    public async Task RunnersList_ReturnsAllRunners()
    {
        var (dispatcher, registry, _) = CreateDispatcher(
            MakeRunnerConfig("runner-a"), MakeRunnerConfig("runner-b"));

        var result = await dispatcher.DispatchAsync("runners list");
        var response = ParseResponse(result);

        Assert.Equal("ok", response.Status);
        Assert.NotNull(response.Data);

        var data = response.Data.Value;
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(2, data.GetArrayLength());

        var names = new[] { data[0].GetProperty("name").GetString(), data[1].GetProperty("name").GetString() };
        Assert.Contains("runner-a", names);
        Assert.Contains("runner-b", names);
    }

    [Fact]
    public async Task RunnersList_IncludesStatusAndId()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync("runners list");
        var entry = ParseResponse(result).Data!.Value[0];

        Assert.Equal(runner.Id, entry.GetProperty("id").GetString());
        Assert.True(entry.TryGetProperty("status", out var status));
        Assert.Equal("starting", status.GetString());
    }

    [Fact]
    public async Task EndpointAllocate_ValidRunner_ReturnsPort()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync(
            $"endpoint allocate {runner.Id} 127.0.0.1");

        var response = ParseResponse(result);
        Assert.Equal("ok", response.Status);

        var data = response.Data!.Value;
        Assert.Equal("127.0.0.1", data.GetProperty("ip").GetString());
        Assert.True(data.GetProperty("port").GetInt32() >= 4900);

        Assert.NotNull(runner.Endpoint);
    }

    [Fact]
    public async Task EndpointAllocate_SameRunner_ReturnsSamePort()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var first = await dispatcher.DispatchAsync(
            $"endpoint allocate {runner.Id} 127.0.0.1");
        var firstPort = ParseResponse(first).Data!.Value.GetProperty("port").GetInt32();

        var second = await dispatcher.DispatchAsync(
            $"endpoint allocate {runner.Id} 127.0.0.1");
        var secondPort = ParseResponse(second).Data!.Value.GetProperty("port").GetInt32();

        Assert.Equal(firstPort, secondPort);
    }

    [Fact]
    public async Task EndpointAllocate_DifferentRunners_GetDifferentPorts()
    {
        var (dispatcher, registry, _) = CreateDispatcher(
            MakeRunnerConfig("runner-a"), MakeRunnerConfig("runner-b"));

        var runnerA = registry.All[0];
        var runnerB = registry.All[1];

        var resultA = await dispatcher.DispatchAsync(
            $"endpoint allocate {runnerA.Id} 127.0.0.1");
        var portA = ParseResponse(resultA).Data!.Value.GetProperty("port").GetInt32();

        var resultB = await dispatcher.DispatchAsync(
            $"endpoint allocate {runnerB.Id} 127.0.0.1");
        var portB = ParseResponse(resultB).Data!.Value.GetProperty("port").GetInt32();

        Assert.NotEqual(portA, portB);
    }

    [Fact]
    public async Task EndpointAllocate_InvalidId_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("endpoint allocate bad-id 127.0.0.1");
        AssertError(result);
    }

    [Fact]
    public async Task EndpointAllocate_UnknownId_ReturnsError()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("endpoint allocate 00000000 127.0.0.1");
        AssertError(result, "unknown");
    }

    [Fact]
    public async Task EndpointAllocate_InvalidIp_ReturnsError()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        var result = await dispatcher.DispatchAsync(
            $"endpoint allocate {runner.Id} not-an-ip");
        AssertError(result, "invalid IP");
    }

    [Fact]
    public async Task RunnersList_IncludesEndpointAfterAllocation()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("runner-a"));
        var runner = registry.All[0];

        await dispatcher.DispatchAsync(
            $"endpoint allocate {runner.Id} 127.0.0.1");

        var result = await dispatcher.DispatchAsync("runners list");
        var entry = ParseResponse(result).Data!.Value[0];

        var endpoint = entry.GetProperty("endpoint").GetString();
        Assert.NotNull(endpoint);
        Assert.Contains("127.0.0.1:", endpoint);
    }

    [Fact]
    public async Task ConfigPath_ReturnsConfiguredFilePath()
    {
        var (dispatcher, _, _) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("config path");
        AssertOk(result);
        var path = ParseResponse(result).Data!.Value.GetProperty("path").GetString();
        Assert.Equal(@"C:\coord\pipe-dispatcher-tests.tw", path);
    }

    [Fact]
    public async Task Quit_InvokesHostShutdown()
    {
        var (dispatcher, _, lifetime) = CreateDispatcher(MakeRunnerConfig());
        var result = await dispatcher.DispatchAsync("quit");
        AssertOk(result);
        Assert.True(lifetime.StopApplicationCalled);
    }

    [Fact]
    public async Task ServiceRegister_ThenFind_ReturnsService()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("r1"));
        var runner = registry.All[0];
        var svc = new ServiceDefinition(
            "t.test.Service",
            ServiceType.Grpc,
            FriendlyName: "x",
            FamilyName: "grp",
            Aliases: new[] { "alias1" },
            Host: "127.0.0.1:2",
            Url: "http://127.0.0.1:2/t.test.Service");
        var reg = await dispatcher.DispatchAsync(
            BuildServiceRegisterCommand(runner.Id, new[] { svc }));
        AssertOk(reg);

        var find = await dispatcher.DispatchAsync("service find t.test.Service");
        AssertOk(find);
        var name = ParseResponse(find).Data!.Value.GetProperty("name").GetString();
        Assert.Equal("t.test.Service", name);
    }

    [Fact]
    public async Task ServiceRegister_ThenList_ReturnsServices()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("r1"));
        var runner = registry.All[0];
        var svc = new ServiceDefinition(
            "t.list.Svc",
            ServiceType.Grpc,
            null,
            "familyA",
            Array.Empty<string>(),
            "127.0.0.1:3",
            "http://127.0.0.1:3/t");
        await dispatcher.DispatchAsync(BuildServiceRegisterCommand(runner.Id, new[] { svc }));

        var list = await dispatcher.DispatchAsync("service list");
        AssertOk(list);
        var data = ParseResponse(list).Data!.Value;
        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Equal("t.list.Svc", data.GetProperty("services")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ServiceFind_MatchesFamilyWhenNameMisses()
    {
        var (dispatcher, registry, _) = CreateDispatcher(MakeRunnerConfig("r1"));
        var runner = registry.All[0];
        var svc = new ServiceDefinition(
            "t.fam.Only",
            ServiceType.Grpc,
            null,
            "sensors",
            Array.Empty<string>(),
            "127.0.0.1:4",
            "http://127.0.0.1:4/x");
        await dispatcher.DispatchAsync(BuildServiceRegisterCommand(runner.Id, new[] { svc }));

        var find = await dispatcher.DispatchAsync("service find sensors");
        AssertOk(find);
        Assert.Equal("t.fam.Only", ParseResponse(find).Data!.Value.GetProperty("name").GetString());
    }
}
