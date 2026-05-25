using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Runlet.Coap.Configuration;
using Tinkwell.Configuration.Parser;
using Tinkwell.Expressions;
using Tinkwell.Integration;

namespace Tinkwell.Runlet.Coap;

/// <summary>
/// Executes the binding chain for a CoAP request: evaluates <c>on</c>-level
/// and <c>bind</c>-level <c>when</c> filters, invokes matching bindings,
/// and returns the last non-null <see cref="BindingResult"/>.
/// </summary>
internal sealed class BindingChainExecutor(
    IReadOnlyDictionary<string, IIntegrationBinding> bindings,
    IExpressionEvaluator evaluator,
    IHostApplicationLifetime lifetime,
    ILogger logger)
{
    private readonly IReadOnlyDictionary<string, IIntegrationBinding> _bindings = bindings;
    private readonly IExpressionEvaluator _evaluator = evaluator;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger _logger = logger;

    private readonly HashSet<string> _disabledBindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Processes a CoAP request through the binding chain for a matched resource.
    /// </summary>
    public async Task<(byte ResponseCode, BindingResult? Result)> ExecuteAsync(
        CoapResourceDefinition resource,
        CoapRequest request,
        CancellationToken ct)
    {
        var method = CoapCode.ToMethodString((byte)request.Method);
        var verbBlocks = resource.VerbBlocks
            .Where(v => string.Equals(v.Verb, method, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (verbBlocks.Count == 0)
            return (CoapCode.MethodNotAllowed, null);

        var context = BuildIntegrationContext(request, method);
        var exprParams = context.ToExpressionParameters();

        var (anyBlockPassed, lastResult) = await RunVerbBlocksAsync(
            verbBlocks, context, exprParams, request.AcceptFormats, ct);

        if (!anyBlockPassed)
            return (CoapCode.MethodNotAllowed, null);

        var code = lastResult is not null
            ? CoapCode.Content
            : DefaultSuccessCode((byte)request.Method);

        return (code, lastResult);
    }

    private static IntegrationContext BuildIntegrationContext(CoapRequest request, string method)
    {
        var payloadString = request.Payload.Length > 0
            ? Encoding.UTF8.GetString(request.Payload.Span)
            : null;

        return new IntegrationContext(
            request.Path,
            request.Query,
            payloadString,
            method)
        {
            PayloadBytes = request.Payload.Length > 0 ? request.Payload.ToArray() : null,
            RequestContentFormat = request.ContentFormat,
            Peer = new PeerIdentity(request.RemoteEndpoint),
        };
    }

    private async Task<(bool AnyBlockPassed, BindingResult? LastResult)> RunVerbBlocksAsync(
        List<CoapVerbBlock> verbBlocks,
        IntegrationContext context,
        IReadOnlyDictionary<string, object?> exprParams,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct)
    {
        BindingResult? lastResult = null;
        bool anyBlockPassed = false;

        foreach (var block in verbBlocks)
        {
            if (!await EvaluateWhenFilterAsync(block.WhenExpression, exprParams, ct))
                continue;

            anyBlockPassed = true;

            foreach (var bindRef in block.Bindings)
            {
                var result = await ExecuteBindingAsync(
                    bindRef, block.OnError, context, exprParams, acceptFormats, ct);

                if (result is not null)
                    lastResult = result;
            }
        }

        return (anyBlockPassed, lastResult);
    }

    private async Task<BindingResult?> ExecuteBindingAsync(
        CoapBindingReference bindRef,
        ErrorPolicy? blockPolicy,
        IntegrationContext context,
        IReadOnlyDictionary<string, object?> exprParams,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        CancellationToken ct)
    {
        if (_disabledBindings.Contains(bindRef.BindingName))
            return null;

        if (!await EvaluateWhenFilterAsync(bindRef.WhenExpression, exprParams, ct))
            return null;

        if (!_bindings.TryGetValue(bindRef.BindingName, out var binding))
        {
            _logger.LogWarning(
                "Binding '{Name}' not found (from '{Assembly}'). Skipping.",
                bindRef.BindingName, bindRef.AssemblyName);
            return null;
        }

        var parameters = new BindingParameterSet(bindRef.Properties, bindRef.NestedBlocks);
        var policy = bindRef.OnError ?? blockPolicy;

        var (result, error) = await InvokeWithRetryAsync(
            binding, context, parameters, acceptFormats, policy, bindRef.BindingName, ct);

        if (error is not null)
        {
            DispatchBindingError(policy, error, bindRef.BindingName);
            return null;
        }

        return result;
    }

    private async Task<(BindingResult? Result, Exception? Error)> InvokeWithRetryAsync(
        IIntegrationBinding binding,
        IntegrationContext context,
        BindingParameterSet parameters,
        IReadOnlyList<CoapContentFormat> acceptFormats,
        ErrorPolicy? policy,
        string bindingName,
        CancellationToken ct)
    {
        var maxAttempts = 1 + (policy?.Retry?.Count ?? 0);

        for (int attempt=0; attempt < maxAttempts; ++attempt)
        {
            try
            {
                var result = binding is ICoapIntegrationBinding coapBinding
                    ? await coapBinding.HandleCoapAsync(context, parameters, _evaluator, acceptFormats, ct)
                    : await binding.HandleAsync(context, parameters, _evaluator, ct);

                return (result, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
            catch (Exception ex)
            {
                if (attempt < maxAttempts - 1)
                {
                    var retry = policy!.Retry!;
                    var delay = (int)(retry.DelayMs * Math.Pow(retry.BackoffMultiplier, attempt));
                    _logger.LogWarning(ex,
                        "Binding '{Name}' failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        bindingName, attempt + 1, retry.Count, delay);
                    await Task.Delay(delay, ct);
                    continue;
                }

                return (null, ex);
            }
        }

        return (null, null);
    }

    private async Task<bool> EvaluateWhenFilterAsync(
        string? whenExpression,
        IReadOnlyDictionary<string, object?> exprParams,
        CancellationToken ct)
    {
        if (whenExpression is null)
            return true;

        return await _evaluator.EvaluateBooleanAsync(
            whenExpression, exprParams, cancellationToken: ct);
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
                    "Binding '{Name}' failed, publish policy not available in CoAP context, resuming",
                    bindingName);
                break;
        }
    }

    private static byte DefaultSuccessCode(byte requestCode) => requestCode switch
    {
        CoapCode.Post => CoapCode.Created,
        CoapCode.Put => CoapCode.Changed,
        CoapCode.Delete => CoapCode.Deleted,
        _ => CoapCode.Content,
    };
}
