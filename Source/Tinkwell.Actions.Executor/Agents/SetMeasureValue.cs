using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Services;

namespace Tinkwell.Actions.Executor.Agents;

[Agent("set_measure")]
sealed class SetMeasureValue(ServiceLocator locator) : IAgent
{
    public sealed class Settings
    {
        [AgentProperty("require")]
        public string Require { get; set; } = "";

        [AgentProperty("name")]
        public string Name { get; set; } = "";

        [AgentProperty("payload")]
        public double Value { get; set; }
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

        using var store = await _locator.FindStoreAsync(cancellationToken);
        await store.Client.UpdateAsync(new()
        {
            Name = _settings.Name,
            Value =
            {
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                NumberValue = _settings.Value
            },
        });
    }

    private readonly Settings _settings = new();
    private readonly ServiceLocator _locator = locator;
}
