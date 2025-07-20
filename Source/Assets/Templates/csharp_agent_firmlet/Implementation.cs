namespace {{namespace}};

sealed class {{name}} : IAsyncDisposable
{
    public {{name}}(ILogger<{{name}}> logger, Options options)
    {
        _logger = logger;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting {{name}}...");

        // TODO: add here your implementation

        _logger.LogInformation("{{name}} started successfully.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await DisposeAsync();

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("{{name}} stopped.");
        return ValueTask.CompletedTask;
    }

    private readonly ILogger<{{name}}> _logger;
    private readonly Options _options;
}
