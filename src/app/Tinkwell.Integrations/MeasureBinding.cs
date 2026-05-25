using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Integration.Measures;

/// <summary>
/// Integration binding for reading and writing measures.
/// <list type="bullet">
///   <item><b>GET</b> — reads the current measure value. Supports content
///     negotiation: <c>text/plain</c> (default) returns the numeric value as
///     a string; <c>application/octet-stream</c> returns a 4-byte IEEE 754
///     big-endian float.</item>
///   <item><b>POST / PUT</b> — sets the measure value from the payload.
///     Produces no output.</item>
///   <item><b>DELETE</b> — no-op, returns <see langword="null"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Required parameter: <c>name</c> (literal or expression).
/// Resolves the measures service via <see cref="IServiceDiscovery"/> (family name <c>measures</c>).
/// The MQTT path only updates values (POST/PUT equivalent); it does not perform GET/DELETE.
/// When the client cannot be obtained, CoAP throws <see cref="InvalidOperationException"/>;
/// MQTT logs a warning and returns <see langword="null"/>.
/// </remarks>
public sealed class MeasureBinding : ICoapIntegrationBinding, IMqttIntegrationBinding
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<MeasureBinding>? _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private MeasuresGrpc.Measures.MeasuresClient? _client;

    public MeasureBinding(IServiceDiscovery discovery, ILogger<MeasureBinding>? logger = null)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "measure";

    public Task<BindingResult?> HandleAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct) =>
        HandleCoapAsync(context, parameters, evaluator, [], ct);

    public async Task<BindingResult?> HandleMqttAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();
        var name = await BindingParameterResolver.ResolveRequiredAsync("name", "Measure", parameters, evaluator, p, ct);
        var client = await GetClientAsync(ct);
        if (client is null)
        {
            _logger?.LogWarning("Measures service not found — skipping");
            return null;
        }
        await HandleSetAsync(client, name, context.Payload, ct);
        return null;
    }

    public async Task<BindingResult?> HandleCoapAsync(
        IntegrationContext context,
        BindingParameterSet parameters,
        IExpressionEvaluator evaluator,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct)
    {
        var p = context.ToExpressionParameters();
        var name = await BindingParameterResolver.ResolveRequiredAsync("name", "Measure", parameters, evaluator, p, ct);
        var client = await GetClientAsync(ct);
        if (client is null)
        {
            _logger?.LogWarning("Measures service not found");
            throw new InvalidOperationException("Measures service not found");
        }

        return context.Method.ToUpperInvariant() switch
        {
            "GET" => await HandleGetAsync(client, name, acceptFormats, ct),
            "POST" or "PUT" => await HandleSetAsync(client, name, context.Payload, ct),
            _ => null,
        };
    }

    private async Task<MeasuresGrpc.Measures.MeasuresClient?> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client is not null)
            {
                return _client;
            }

            try
            {
                var svc = await _discovery.DiscoverAsync("measures", ct);

                if (svc is null)
                {
                    return null;
                }

                _client = await _discovery.CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, ct);
                return _client;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to discover measures service");
                return null;
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private static async Task<BindingResult?> HandleGetAsync(
        MeasuresGrpc.Measures.MeasuresClient client,
        string name, IReadOnlyList<CoapContentFormat> acceptFormats, CancellationToken ct)
    {
        var response = await client.GetAsync(
            new MeasuresGrpc.GetMeasureRequest { Name = name },
            cancellationToken: ct);

        if (!response.Found)
        {
            throw new ArgumentException($"Measure '{name}' not found");
        }

        var value = response.Measure.Value.NumericValue;

        if (acceptFormats.Contains(CoapContentFormat.ApplicationOctetStream))
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteSingleBigEndian(bytes, (float)value);
            return new BindingResult(bytes, CoapContentFormat.ApplicationOctetStream);
        }

        var text = value.ToString(CultureInfo.InvariantCulture);
        return new BindingResult(Encoding.UTF8.GetBytes(text), CoapContentFormat.TextPlain);
    }

    private static async Task<BindingResult?> HandleSetAsync(
        MeasuresGrpc.Measures.MeasuresClient client,
        string name, string? payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required to set a measure value");
        }

        if (!double.TryParse(payload.Trim(), CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Cannot parse '{payload}' as a numeric value");
        }

        await client.UpdateAsync(
            new MeasuresGrpc.UpdateMeasureRequest
            {
                Name = name,
                Value = new MeasuresGrpc.MeasureValueProto
                {
                    Type = "number",
                    NumericValue = value,
                }
            },
            cancellationToken: ct);

        return null;
    }
}