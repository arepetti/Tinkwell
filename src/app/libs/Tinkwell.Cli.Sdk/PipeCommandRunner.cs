using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Tinkwell.Cli;

/// <summary>
/// Sends a single command to the coordinator's named pipe and returns the
/// parsed JSONL response. Each call opens a fresh connection.
/// </summary>
public static class PipeCommandRunner
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Sends <paramref name="command"/> and returns the response envelope.
    /// </summary>
    public static async Task<PipeResult> SendAsync(
        TwCoordinatorSettings settings, string command, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        await using var pipe = new NamedPipeClientStream(
            settings.Machine, settings.PipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        await pipe.ConnectAsync(cts.Token);

        await using var writer = new StreamWriter(pipe, Utf8NoBom, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
        using var reader = new StreamReader(pipe, Utf8NoBom, leaveOpen: true);

        await writer.WriteLineAsync(command.AsMemory(), cts.Token);
        var line = await reader.ReadLineAsync(cts.Token);
        sw.Stop();

        if (string.IsNullOrWhiteSpace(line))
            return new PipeResult("error", "Empty response from coordinator", null, sw.Elapsed);

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        var status = root.GetProperty("status").GetString() ?? "error";
        string? message = root.TryGetProperty("message", out var msgEl)
            ? msgEl.GetString() : null;
        JsonElement? data = root.TryGetProperty("data", out var dataEl)
            ? dataEl.Clone() : null;

        return new PipeResult(status, message, data, sw.Elapsed);
    }

    /// <summary>
    /// Convenience: sends, checks for success, and returns <c>data</c>.
    /// Throws on error.
    /// </summary>
    public static async Task<JsonElement?> SendOkAsync(
        TwCoordinatorSettings settings, string command, CancellationToken ct = default)
    {
        var result = await SendAsync(settings, command, ct);
        result.EnsureSuccess();
        return result.Data;
    }

    /// <summary>Deserialize a <see cref="JsonElement"/> to <typeparamref name="T"/>.</summary>
    public static T? Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(JsonOptions);
}

/// <summary>
/// Parsed JSONL response from the coordinator pipe.
/// </summary>
public sealed record PipeResult(
    string Status,
    string? Message,
    JsonElement? Data,
    TimeSpan Latency)
{
    /// <summary>Whether the coordinator reported <c>"ok"</c> status.</summary>
    public bool IsOk => string.Equals(Status, "ok", StringComparison.OrdinalIgnoreCase);

    /// <summary>Throws <see cref="TwCommandException"/> when the result is not <c>"ok"</c>.</summary>
    public void EnsureSuccess()
    {
        if (!IsOk)
            throw new TwCommandException(Message ?? "Unknown coordinator error");
    }
}
