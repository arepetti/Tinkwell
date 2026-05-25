using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Mqtt.Configuration;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Executes the binding chain for an MQTT message: evaluates <c>on</c>-level
/// and <c>bind</c>-level <c>when</c> filters, invokes matching bindings.
/// Uses <see cref="IMqttIntegrationBinding.HandleMqttAsync"/> when the binding
/// implements it, otherwise <see cref="IIntegrationBinding.HandleAsync"/>.
/// </summary>
internal sealed class MqttBindingChainExecutor
{
    private readonly IReadOnlyDictionary<string, IIntegrationBinding> _bindings;
    private readonly IExpressionEvaluator _evaluator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger _logger;

    private readonly HashSet<string> _disabledBindings = new(StringComparer.OrdinalIgnoreCase);

    public MqttBindingChainExecutor(
        IReadOnlyDictionary<string, IIntegrationBinding> bindings,
        IExpressionEvaluator evaluator,
        IHostApplicationLifetime lifetime,
        ILogger logger)
    {
        _bindings = bindings;
        _evaluator = evaluator;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Processes an MQTT message through the binding chain for the given subscription.
    /// </summary>
    public async Task ExecuteAsync(
        MqttSubscriptionDefinition subscription,
        string topic,
        string payload,
        CancellationToken ct)
    {
        var context = new IntegrationContext(topic, null, payload, "MESSAGE");
        var exprParams = context.ToExpressionParameters();

        foreach (var block in subscription.VerbBlocks)
        {
            if (!string.Equals(block.Verb, "message", StringComparison.OrdinalIgnoreCase))
                continue;

            if (block.WhenExpression is not null)
            {
                var passes = await _evaluator.EvaluateBooleanAsync(
                    block.WhenExpression, exprParams, cancellationToken: ct);
                if (!passes)
                    continue;
            }

            foreach (var bindRef in block.Bindings)
            {
                if (_disabledBindings.Contains(bindRef.BindingName))
                    continue;

                if (bindRef.WhenExpression is not null)
                {
                    var passes = await _evaluator.EvaluateBooleanAsync(
                        bindRef.WhenExpression, exprParams, cancellationToken: ct);
                    if (!passes)
                        continue;
                }

                if (!_bindings.TryGetValue(bindRef.BindingName, out var binding))
                {
                    _logger.LogWarning(
                        "Binding '{Name}' not found (from '{Assembly}'). Skipping.",
                        bindRef.BindingName, bindRef.AssemblyName);
                    continue;
                }

                var parameters = new BindingParameterSet(bindRef.Properties, bindRef.NestedBlocks);
                var policy = bindRef.OnError ?? block.OnError;
                var maxAttempts = 1 + (policy?.Retry?.Count ?? 0);
                Exception? lastEx = null;

                for (int attempt=0; attempt < maxAttempts; ++attempt)
                {
                    try
                    {
                        if (binding is IMqttIntegrationBinding mqttBinding)
                        {
                            await mqttBinding.HandleMqttAsync(context, parameters, _evaluator, ct);
                        }
                        else
                        {
                            await binding.HandleAsync(context, parameters, _evaluator, ct);
                        }
                        lastEx = null;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        if (attempt < maxAttempts - 1)
                        {
                            var retry = policy!.Retry!;
                            var delay = (int)(retry.DelayMs * Math.Pow(retry.BackoffMultiplier, attempt));
                            _logger.LogWarning(ex,
                                "Binding '{Name}' failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                                bindRef.BindingName, attempt + 1, retry.Count, delay);
                            await Task.Delay(delay, ct);
                        }
                    }
                }

                if (lastEx is not null)
                    DispatchBindingError(policy, lastEx, bindRef.BindingName);
            }
        }
    }

    private void DispatchBindingError(ErrorPolicy? policy, Exception ex, string bindingName)
    {
        var action = policy?.Action ?? ErrorPolicyAction.ResumeNext;

        switch (action)
        {
            case ErrorPolicyAction.ResumeNext:
                _logger.LogWarning(ex, "Binding '{Name}' failed, resuming", bindingName);
                break;
            case ErrorPolicyAction.StopThis:
                _logger.LogError(ex, "Binding '{Name}' failed, disabling", bindingName);
                _disabledBindings.Add(bindingName);
                break;
            case ErrorPolicyAction.StopApplication:
                _logger.LogCritical(ex, "Binding '{Name}' failed, stopping application", bindingName);
                _lifetime.StopApplication();
                break;
            case ErrorPolicyAction.Publish:
                _logger.LogWarning(ex,
                    "Binding '{Name}' failed, publish policy not available in MQTT context, resuming",
                    bindingName);
                break;
        }
    }
}