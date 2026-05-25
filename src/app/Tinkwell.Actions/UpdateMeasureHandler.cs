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
/// External action handler that updates a measure value via the measures
/// gRPC service.
/// </summary>
/// <remarks>
/// Parameters:
/// <list type="bullet">
///   <item><c>name</c> (required) — the measure name.</item>
///   <item><c>value</c> (required) — the new value (numeric or string).</item>
/// </list>
/// </remarks>
public sealed class UpdateMeasureHandler : IActionHandler
{
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<UpdateMeasureHandler> _logger;

    public UpdateMeasureHandler(IServiceDiscovery discovery, ILogger<UpdateMeasureHandler> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    public string Name => "update-measure";

    public async Task ExecuteAsync(
        EventEnvelope trigger,
        IReadOnlyDictionary<string, ConfigValue> parameters,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        var name = await ActionParameterResolver.ResolveRequiredAsync(
            "name", parameters, trigger, evaluator, cancellationToken);
        var valueStr = await ActionParameterResolver.ResolveRequiredAsync(
            "value", parameters, trigger, evaluator, cancellationToken);

        var client = await GetClientAsync(cancellationToken);
        if (client is null)
            return;

        var measureValue = new MeasuresGrpc.MeasureValueProto();
        if (double.TryParse(valueStr, CultureInfo.InvariantCulture, out var numericValue))
        {
            measureValue.Type = "number";
            measureValue.NumericValue = numericValue;
        }
        else
        {
            measureValue.Type = "string";
            measureValue.StringValue = valueStr;
        }

        await client.UpdateAsync(
            new MeasuresGrpc.UpdateMeasureRequest { Name = name, Value = measureValue },
            cancellationToken: cancellationToken);

        _logger.LogDebug("update-measure: set '{Name}' = {Value}", name, valueStr);
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