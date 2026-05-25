using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Tinkwell.Pipes;
using Tinkwell.Telemetry;

namespace Tinkwell.Runner.Hosting;

/// <summary>
/// Client for communicating with the coordinator's command pipe.
/// Each method opens a fresh connection, sends a command, reads the
/// JSONL response, and closes.
/// </summary>
public sealed class CoordinatorPipeClient : PipeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public CoordinatorPipeClient(string pipeName, ILogger logger, int timeoutMs = 10_000)
        : base(pipeName, logger, timeoutMs)
    {
    }

    /// <summary>
    /// Sends a raw command line and returns the parsed response envelope.
    /// The timeout covers the entire operation (connect + write + read).
    /// </summary>
    public async Task<PipeResponse> SendAsync(string command, CancellationToken cancellationToken = default)
    {
        using var activity = OtTraces.Source.Start(OtTraces.PipeClientSend,
            (OtTraces.Command, command.Split(' ', 2)[0]));

        Logger.LogTrace("Sending pipe command: {Command}", command);

        var responseLine = await SendLineAsync(command, cancellationToken);

        if (string.IsNullOrWhiteSpace(responseLine))
            throw new IOException("Empty response from coordinator");

        Logger.LogTrace("Pipe response: {Response}", responseLine);

        return JsonSerializer.Deserialize<PipeResponse>(responseLine, JsonOptions)
            ?? throw new IOException("Failed to deserialize coordinator response");
    }

    /// <summary>
    /// Fetches the fully-qualified path of the configuration file loaded
    /// by the coordinator at startup via <c>config path</c>.
    /// </summary>
    public async Task<string> FetchConfigPathAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("config path", cancellationToken);
        response.EnsureSuccess();

        if (response.Data is not { ValueKind: JsonValueKind.Object } data)
            throw new IOException("Unexpected config path response shape");

        return data.GetProperty("path").GetString()
            ?? throw new IOException("Missing 'path' in config path response");
    }

    /// <summary>
    /// Fetches this runner's identity and its runlet descriptors from the
    /// coordinator via <c>config read</c>. The response includes the runner's
    /// name and settings alongside its runlet list.
    /// </summary>
    public async Task<(RunnerDescriptor Identity, RunletDescriptor[] Runlets)> FetchRunnerConfigAsync(
        string runnerId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync($"config read {runnerId}", cancellationToken);
        response.EnsureSuccess();

        if (response.Data is not { ValueKind: JsonValueKind.Object } data)
            throw new IOException("Unexpected config read response shape");

        var name = data.GetProperty("name").GetString()
            ?? throw new IOException("Missing 'name' in config read response");

        var settings = data.TryGetProperty("settings", out var settingsEl)
            ? settingsEl.Deserialize<Dictionary<string, string>>(JsonOptions) ?? []
            : new Dictionary<string, string>();

        var runlets = data.TryGetProperty("runlets", out var runletsEl)
            ? runletsEl.Deserialize<RunletDescriptor[]>(JsonOptions) ?? []
            : [];

        var identity = new RunnerDescriptor(runnerId, name, settings);
        return (identity, runlets);
    }

    /// <summary>
    /// Notifies the coordinator that this runner is ready.
    /// </summary>
    public async Task NotifyReadyAsync(string runnerId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync($"notify ready {runnerId}", cancellationToken);
        response.EnsureSuccess();
    }

    /// <summary>
    /// Requests an endpoint (IP + port) from the coordinator for this runner.
    /// If the runner already has an assigned endpoint (e.g. from a previous
    /// run), the same port is returned.
    /// </summary>
    public async Task<IPEndPoint> AllocateEndpointAsync(
        string runnerId, IPAddress listenAddress, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            $"endpoint allocate {runnerId} {listenAddress}", cancellationToken);
        response.EnsureSuccess();

        if (response.Data is not { ValueKind: JsonValueKind.Object } data)
            throw new IOException("Unexpected endpoint allocate response shape");

        var ip = data.GetProperty("ip").GetString()
            ?? throw new IOException("Missing 'ip' in endpoint allocate response");
        var port = data.GetProperty("port").GetInt32();

        return new IPEndPoint(IPAddress.Parse(ip), port);
    }

    /// <summary>
    /// Notifies the coordinator of a fatal error.
    /// </summary>
    public async Task NotifyFatalAsync(string runnerId, string message, CancellationToken cancellationToken = default)
    {
        var escaped = SanitizeForPipeQuotedArgument(message);
        var response = await SendAsync($"notify fatal {runnerId} \"{escaped}\"", cancellationToken);
        response.EnsureSuccess();
    }

    /// <summary>
    /// Strips line breaks and other control characters (they would break a
    /// single-line protocol command) and backslash-escapes for embedding in
    /// double-quoted arguments.
    /// </summary>
    private static string SanitizeForPipeQuotedArgument(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var sb = new StringBuilder(message.Length);
        foreach (var c in message)
        {
            if (char.IsControl(c))
                sb.Append(' ');
            else
                sb.Append(c);
        }

        return sb
            .ToString()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// Registers the runner's service definitions with the coordinator so
    /// they are discoverable via <c>service find</c> / <c>service list</c>.
    /// </summary>
    public async Task RegisterServicesAsync(
        string runnerId,
        IReadOnlyList<ServiceDefinition> services,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(services, JsonOptions);
        var response = await SendAsync(
            $"service register {runnerId} \"{json.Replace("\"", "\\\"")}\"",
            cancellationToken);
        response.EnsureSuccess();
    }

    /// <summary>
    /// Queries the coordinator's service registry for a service by name,
    /// alias, or family name. Returns <see langword="null"/> if not found.
    /// </summary>
    public async Task<ServiceDefinition?> FindServiceAsync(
        string name, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync($"service find {name}", cancellationToken);

        if (!response.IsOk)
            return null;

        if (response.Data is not { ValueKind: JsonValueKind.Object } data)
            return null;

        return data.Deserialize<ServiceDefinition>(JsonOptions);
    }

    /// <summary>
    /// Lists all services registered with the coordinator, optionally
    /// filtered by a query string.
    /// </summary>
    public async Task<ServiceDefinition[]> ListServicesAsync(
        string? query = null, CancellationToken cancellationToken = default)
    {
        var command = string.IsNullOrWhiteSpace(query)
            ? "service list"
            : $"service list {query}";

        var response = await SendAsync(command, cancellationToken);
        response.EnsureSuccess();

        if (response.Data is not { ValueKind: JsonValueKind.Object } data)
            return [];

        if (!data.TryGetProperty("services", out var servicesEl))
            return [];

        return servicesEl.Deserialize<ServiceDefinition[]>(JsonOptions) ?? [];
    }
}

/// <summary>
/// Deserialized JSONL envelope from the coordinator.
/// </summary>
public sealed record PipeResponse(
    string Status,
    string? Message,
    JsonElement? Data)
{
    public bool IsOk => string.Equals(Status, "ok", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Throws if the response status is not <c>ok</c>.
    /// </summary>
    public void EnsureSuccess()
    {
        if (!IsOk)
            throw new IOException($"Coordinator command failed: {Message ?? "unknown error"}");
    }
}
