using System.Globalization;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Events;
using Tinkwell.Expressions;
using Tinkwell.Runner;
using Tinkwell.Actions.Abstractions;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Actions.Measures;

/// <summary>
/// External action handler that creates a new measure definition via the
/// measures gRPC service.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>name</c> (required) — the measure name.</item>
///   <item><c>quantity</c> (optional) — the quantity type (e.g. "Temperature").</item>
///   <item><c>unit</c> (optional) — the unit (e.g. "Celsius").</item>
///   <item><c>value</c> (optional) — initial numeric value.</item>
/// </list>
/// </remarks>
public sealed class CreateMeasureHandler : IActionHandler
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<CreateMeasureHandler> _logger;

    public CreateMeasureHandler(IServiceDiscovery discovery, ILogger<CreateMeasureHandler> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "create-measure";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        var name = await ActionParameterResolver.ResolveRequiredAsync(
            "name", parameters, trigger, evaluator, cancellationToken);
        var quantity = await ActionParameterResolver.ResolveOptionalAsync(
            "quantity", parameters, trigger, evaluator, cancellationToken);
        var unit = await ActionParameterResolver.ResolveOptionalAsync(
            "unit", parameters, trigger, evaluator, cancellationToken);
        var valueStr = await ActionParameterResolver.ResolveOptionalAsync(
            "value", parameters, trigger, evaluator, cancellationToken);

        var client = await GetClientAsync(cancellationToken);
        if (client is null)
            return;

        var request = new MeasuresGrpc.RegisterMeasureRequest
        {
            Definition = new MeasuresGrpc.MeasureDefinitionProto
            {
                Name = name,
                QuantityType = quantity ?? string.Empty,
                Unit = unit ?? string.Empty,
            }
        };

        if (valueStr is not null && double.TryParse(valueStr, CultureInfo.InvariantCulture, out var numericValue))
        {
            request.InitialValue = new MeasuresGrpc.MeasureValueProto
            {
                Type = "number",
                NumericValue = numericValue,
                Unit = unit ?? string.Empty,
            };
        }

        await client.RegisterAsync(request, cancellationToken: cancellationToken);
        _logger.LogDebug("create-measure: registered '{Name}'", name);
    }

    private async Task<MeasuresGrpc.Measures.MeasuresClient?> GetClientAsync(CancellationToken ct)
    {
        try
        {
            var svc = await _discovery.DiscoverAsync("measures", ct);

            if (svc is null)
            {
                _logger.LogWarning("Measures service not found");
                return null;
            }

            var channel = GrpcChannel.ForAddress(svc.Url);
            return new MeasuresGrpc.Measures.MeasuresClient(channel);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover measures service");
            return null;
        }
    }
}