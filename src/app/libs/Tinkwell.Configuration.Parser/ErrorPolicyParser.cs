using Tinkwell.Configuration;

namespace Tinkwell.Configuration.Parser;

/// <summary>
/// Parses an <c>on error</c> <see cref="ConfigBlock"/> into a typed
/// <see cref="ErrorPolicy"/>. Shared by all domain parsers (actions,
/// CoAP, MQTT, measures).
/// </summary>
/// <remarks>
/// <para>Expects <c>block.Type</c> and <c>block.Name</c> to form <c>on error</c>, with
/// modifiers such as <c>resume next</c>, <c>stop this</c>, or <c>publish "event-name"</c>,
/// and optional <c>retry</c> / <c>delay</c> / <c>backoff</c> (or a braced <c>publish</c> body
/// for extra event properties). Examples as written in a <c>.tw</c> file:</para>
/// <code>
/// on error resume next;
/// on error stop this retry 3 delay 500 backoff 2;
/// on error publish "my-event" retry 2 delay 1000 {
///     key = "value"
/// }
/// </code>
/// </remarks>
public static class ErrorPolicyParser
{
    /// <summary>
    /// Parses a <see cref="ConfigBlock"/> with <c>Type == "on"</c> and
    /// <c>Name == "error"</c> into an <see cref="ErrorPolicy"/>.
    /// </summary>
    /// <param name="block">The parsed <c>on error ...</c> config block.</param>
    /// <returns>A fully resolved <see cref="ErrorPolicy"/>.</returns>
    /// <exception cref="ConfigurationSyntaxException">
    /// The block contains unknown or conflicting modifiers.
    /// </exception>
    public static ErrorPolicy Parse(ConfigBlock block)
    {
        ErrorPolicyAction? action = null;
        string? eventName = null;
        int? retryCount = null;
        int? delayMs = null;
        double? backoff = null;

        foreach (var mod in block.Modifiers)
        {
            switch (mod.Key)
            {
                case "resume":
                {
                    var val = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    if (!string.Equals(val, "next", StringComparison.OrdinalIgnoreCase))
                        throw SyntaxError($"Expected 'resume next', got 'resume {val}'.", block);
                    if (action is not null)
                        throw SyntaxError("Multiple error actions specified.", block);
                    action = ErrorPolicyAction.ResumeNext;
                    break;
                }
                case "stop":
                {
                    var val = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    if (string.Equals(val, "this", StringComparison.OrdinalIgnoreCase))
                    {
                        if (action is not null)
                            throw SyntaxError("Multiple error actions specified.", block);
                        action = ErrorPolicyAction.StopThis;
                    }
                    else if (string.Equals(val, "application", StringComparison.OrdinalIgnoreCase))
                    {
                        if (action is not null)
                            throw SyntaxError("Multiple error actions specified.", block);
                        action = ErrorPolicyAction.StopApplication;
                    }
                    else
                    {
                        throw SyntaxError(
                            $"Expected 'stop this' or 'stop application', got 'stop {val}'.", block);
                    }
                    break;
                }
                case "publish":
                {
                    if (action is not null)
                        throw SyntaxError("Multiple error actions specified.", block);
                    action = ErrorPolicyAction.Publish;
                    eventName = ConfigValueConverter.ConvertTo<string>(mod.Value, block.Location);
                    break;
                }
                case "retry":
                    retryCount = ConfigValueConverter.ConvertTo<int>(mod.Value, block.Location);
                    if (retryCount < 0)
                        throw SyntaxError("Retry count must be non-negative.", block);
                    break;
                case "delay":
                    delayMs = ConfigValueConverter.ConvertTo<int>(mod.Value, block.Location);
                    if (delayMs < 0)
                        throw SyntaxError("Delay must be non-negative.", block);
                    break;
                case "backoff":
                    backoff = ConfigValueConverter.ConvertTo<double>(mod.Value, block.Location);
                    if (backoff < 0)
                        throw SyntaxError("Backoff multiplier must be non-negative.", block);
                    break;
                default:
                    throw SyntaxError(
                        $"Unknown modifier '{mod.Key}' on 'on error'. " +
                        "Expected 'resume', 'stop', 'publish', 'retry', 'delay', or 'backoff'.", block);
            }
        }

        if (action is null)
            throw SyntaxError(
                "Missing error action. Expected 'resume next', 'stop this', " +
                "'stop application', or 'publish \"event-name\"'.", block);

        if (action != ErrorPolicyAction.Publish && eventName is not null)
            throw SyntaxError("Event name is only valid with 'publish'.", block);

        RetryPolicy? retry = null;
        if (retryCount is > 0)
        {
            retry = new RetryPolicy(
                retryCount.Value,
                delayMs ?? 1000,
                backoff ?? 1.0);
        }
        else if (delayMs is not null || backoff is not null)
        {
            throw SyntaxError("'delay' and 'backoff' require 'retry N' with N > 0.", block);
        }

        IReadOnlyDictionary<string, ConfigValue>? eventProperties = null;
        if (action == ErrorPolicyAction.Publish && block.Properties.Count > 0)
        {
            var props = new Dictionary<string, ConfigValue>(
                block.Properties.Count, StringComparer.Ordinal);
            foreach (var prop in block.Properties)
                props[prop.Key] = prop.Value;
            eventProperties = props;
        }

        return new ErrorPolicy(action.Value, retry, eventName, eventProperties);
    }

    private static ConfigurationSyntaxException SyntaxError(string message, ConfigBlock block) =>
        new(message, block.Location.FilePath, block.Location.Line, block.Location.Column);
}
