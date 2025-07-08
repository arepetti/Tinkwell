
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Tinkwell.Bootstrapper;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Supervisor.Commands;

namespace Tinkwell.Supervisor;

sealed class Registry(ILogger<Registry> logger, IConfigFileReader<IEnsambleFile> reader, IChildProcessBuilder processBuilder) : IRegistry
{
    public IEnumerable<IChildProcess> Items => _items;

    public async Task StartAsync(ICommandServer commandServer, string configurationPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting registry with configuration: {ConfigurationPath}", configurationPath);
        _logger.LogTrace("OS: {OsDescription} ({OsArchitecture}), Process: {ProcessArchitecture}",
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture);

        _logger.LogTrace("Loading ensamble definitions from {ConfigurationPath}", configurationPath);
        var file = await _reader.ReadAsync(configurationPath, cancellationToken);
        _items = [.. file.Runners.Select(_processBuilder.Create)];

        if (cancellationToken.IsCancellationRequested)
            return;

        var barrier = new SignalBarrier();
        commandServer.Signaled += barrier.HandleSignal;

        ForEach(nameof(IChildProcess.Start), async (item) =>
        {
            item.Start();
            if (item.Definition.IsBlockingActivation())
                await barrier.WaitAndResetAsync();
        });
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

    public IEnumerable<IChildProcess> FindAllByQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _items;

        return _items
            .Where(x => x.Definition.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

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

    private const int WarningIfOperationOnProcessIsLongerThanMilliseconds = 10000;

    private readonly ILogger<Registry> _logger = logger;
    private readonly IConfigFileReader<IEnsambleFile> _reader = reader;
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

                Stopwatch stopwatch = new();
                stopwatch.Start();

                action(item);

                stopwatch.Stop();
                _logger.LogDebug("'{ActionName}' on {ItemName} took {ElapsedMilliseconds} ms",
                    actionName, item.Definition.Name, stopwatch.ElapsedMilliseconds);

                if (stopwatch.ElapsedMilliseconds > WarningIfOperationOnProcessIsLongerThanMilliseconds)
                {
                    _logger.LogWarning("'{ActionName}' on {ItemName} took too long to complete ({ElapsedMilliseconds} ms)",
                        actionName, item.Definition.Name, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (BootstrapperException e)
            {
                _logger.LogError(e, "Error performing '{ActionName} on {ItemName}", actionName, item.Definition.Name);
            }
        }
        _logger.LogDebug("Applied '{ActionName}' on {Count} child processes", actionName, _items.Count());
    }

    private void ForEach(string actionName, Func<IChildProcess, Task> action)
        => ForEach(actionName, item => action(item).GetAwaiter().GetResult());
}

file class SignalBarrier
{
    public async Task WaitAndResetAsync()
    {
        await _tcs.Task;
        _tcs = new();
    }

    public void HandleSignal(object? sender, EventArgs e) => _tcs.TrySetResult();

    private TaskCompletionSource _tcs = new();
}