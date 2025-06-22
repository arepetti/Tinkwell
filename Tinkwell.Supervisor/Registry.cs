
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;

namespace Tinkwell.Supervisor;

sealed class Registry(ILogger<Registry> logger, IEnsambleFileReader reader, IChildProcessBuilder processBuilder) : IRegistry
{
    public IEnumerable<IChildProcess> Items => _items;

    public async Task StartAsync(string configurationPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting registry with configuration: {ConfigurationPath}", configurationPath);
        _logger.LogTrace("OS: {OsDescription} ({OsArchitecture}), Process: {ProcessArchitecture}",
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture);

        _logger.LogTrace("Loading ensamble definitions from {ConfigurationPath}", configurationPath);
        var definitions = await _reader.ReadAsync(configurationPath, cancellationToken);
        _items = [.. definitions.Select(_processBuilder.Create)];

        if (cancellationToken.IsCancellationRequested)
            return;

        ForEach(nameof(IChildProcess.Start), item => item.Start());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping registry");
        ForEach(nameof(IChildProcess.Stop), item => item.Stop());

        return Task.CompletedTask;
    }

    public IChildProcess? FindByName(string name)
        => _items.FirstOrDefault(x => string.Equals(x.Definition.Name, name, StringComparison.Ordinal));

    public IChildProcess? FindById(int id)
        => _items.FirstOrDefault(x => x.Id == id);

    public void AddNew(RunnerDefinition definition, bool start)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (FindByName(definition.Name) is not null)
            throw new BootstrapperException($"A runner with the name '{definition.Name}' already exists.");

        var process = _processBuilder.Create(definition);
        _items.Add(process);

        if (start)
            process.Start();
    }

    private readonly ILogger<Registry> _logger = logger;
    private readonly IEnsambleFileReader _reader = reader;
    private readonly IChildProcessBuilder _processBuilder = processBuilder;
    private List<IChildProcess> _items = [];

    void ForEach(string actionName, Action<IChildProcess> action)
    {
        _logger.LogDebug("About to perform '{ActionName}' on {Count} child processes", actionName, _items.Count());
        foreach (var item in _items)
        {
            try
            {
                _logger.LogTrace("Performing '{ActionName}' on {ItemName}", actionName, item.Definition.Name);
                action(item);
            }
            catch (BootstrapperException e)
            {
                _logger.LogError(e, "Error performing '{ActionName} on {ItemName}", actionName, item.Definition.Name);
            }
        }
        _logger.LogInformation("Applied '{ActionName}' on {Count} child processes", actionName, _items.Count());
    }
}