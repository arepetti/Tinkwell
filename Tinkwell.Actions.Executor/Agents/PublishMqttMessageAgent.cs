using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Services;

namespace Tinkwell.Actions.Executor.Agents;

[Agent("mqtt_publish")]
public sealed class PublishMqttMessageAgent(ILogger<PublishMqttMessageAgent> logger, ServiceLocator locator) : IAgent
{
    public sealed class Settings
    {
        [AgentProperty("require")]
        public string Require { get; set; } = "";

        [AgentProperty("topic")]
        public string Topic { get; set; } = "";

        [AgentProperty("payload")]
        public string Payload { get; set; } = "";
    }

    object? IAgent.Settings => _settings;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Require))
        {
            var expr = new ExpressionEvaluator();
            if (expr.EvaluateBool(_settings.Require, null) == false)
                return;
        }

        using var bridge = await _locator.FindServiceAsync(
            MqttClient.Descriptor.FullName,
            c => new MqttClient.MqttClientClient(c),
            cancellationToken
        );

        var result = await bridge.Client.PublishAsync(
            new() { Topic = _settings.Topic, Payload = _settings.Payload },
            cancellationToken: cancellationToken
        );

        if (result.Status == PublishMqttMessageResponse.Types.Status.Error)
            _logger.LogWarning("Failed to send MQTT message to {Topic}.", _settings.Topic);
    }

    private readonly Settings _settings = new();
    private readonly ILogger<PublishMqttMessageAgent> _logger = logger;
    private readonly ServiceLocator _locator = locator;
}
