using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Tinkwell.Coordinator.Pipes;

/// <summary>
/// Provides output and service access for pipe commands. Each pipe
/// connection receives its own context instance.
/// </summary>
/// <remarks>
/// <para>
/// Every response is a single JSONL envelope:
/// <c>{"status":"ok"}</c>, <c>{"status":"ok","data":{...}}</c>,
/// or <c>{"status":"error","message":"..."}</c>.
/// </para>
/// <para>
/// Commands signal their result through <see cref="WriteSuccess()"/>,
/// <see cref="WriteSuccess(string)"/>, <see cref="WriteSuccess{T}(T)"/>,
/// or <see cref="WriteError"/>. When no method is called and the command
/// exits successfully, the default response is <c>{"status":"ok"}</c>.
/// </para>
/// </remarks>
internal sealed class PipeCommandContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IServiceProvider _services;
    private string? _response;

    public PipeCommandContext(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Signals success with no additional data.
    /// Produces <c>{"status":"ok"}</c>.
    /// </summary>
    public void WriteSuccess() =>
        _response = SerializeEnvelope("ok", null, null);

    /// <summary>
    /// Signals success with a human-readable message.
    /// Produces <c>{"status":"ok","message":"..."}</c>.
    /// </summary>
    public void WriteSuccess(string message) =>
        _response = SerializeEnvelope("ok", message, null);

    /// <summary>
    /// Signals success with a data payload serialized as JSON.
    /// Produces <c>{"status":"ok","data":{...}}</c>.
    /// </summary>
    public void WriteSuccess<T>(T data) =>
        _response = SerializeEnvelope("ok", null, JsonSerializer.SerializeToElement(data, JsonOptions));

    /// <summary>
    /// Signals an error with a descriptive message.
    /// Produces <c>{"status":"error","message":"..."}</c>.
    /// </summary>
    public void WriteError(string message) =>
        _response = SerializeEnvelope("error", message, null);

    /// <summary>
    /// Resolves a required service from the coordinator's DI container.
    /// </summary>
    public T GetService<T>() where T : notnull =>
        _services.GetRequiredService<T>();

    /// <summary>
    /// Resolves an optional service from the coordinator's DI container.
    /// </summary>
    public T? FindService<T>() where T : class =>
        _services.GetService<T>();

    /// <summary>
    /// Whether a <c>WriteSuccess</c> or <c>WriteError</c> method was called.
    /// Used by the dispatcher to detect silent command failures.
    /// </summary>
    public bool HasExplicitResponse => _response is not null;

    /// <summary>
    /// Returns the JSONL response envelope. Defaults to
    /// <c>{"status":"ok"}</c> when no write method was called.
    /// </summary>
    public string GetResponse() =>
        _response ?? SerializeEnvelope("ok", null, null);

    /// <summary>
    /// Creates a JSONL error envelope without requiring a context instance.
    /// Used by the dispatcher for infrastructure-level errors.
    /// </summary>
    public static string ErrorEnvelope(string message) =>
        SerializeEnvelope("error", message, null);

    private static string SerializeEnvelope(string status, string? message, JsonElement? data)
    {
        var envelope = new PipeResponseEnvelope(status, message, data);
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private sealed record PipeResponseEnvelope(
        string Status,
        string? Message,
        JsonElement? Data);
}
