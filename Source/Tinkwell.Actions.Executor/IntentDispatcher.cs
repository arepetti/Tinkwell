using Microsoft.Extensions.Logging;

namespace Tinkwell.Actions.Executor;

sealed class IntentDispatcher(IServiceProvider serviceProvider, ILogger<IntentDispatcher> logger, AgentFactory factory) : IIntentDispatcher
{
    public async Task DispatchAsync(Intent intent, CancellationToken cancellationToken)
    {
        try
        {
            IAgent? agent = null;
            try
            {
                agent = _factory.Create(_serviceProvider, intent);
            }
            catch (ExecutorException e)
            {
                _logger.LogError(e, "Disabling listener because it failed to create agent for intent '{IntentId}': {Message}.", intent.Event.Id, e.Message);
                intent.Listener.Enabled = false;
            }

            if (agent is not null)
                await agent.ExecuteAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An unexpected error occurred while executing the intent '{IntentId}': {Message}.", intent.Event.Id, e.Message);
        }
    }

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<IntentDispatcher> _logger = logger;
    private readonly AgentFactory _factory = factory;
}
