using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Runlet.Modbus.Configuration;
using Tinkwell.Modbus;
using Tinkwell.Runner;
using MeasuresGrpc = Tinkwell.Runlet.Measures.Grpc.V1;

namespace Tinkwell.Runlet.Modbus;

/// <summary>
/// Parses Modbus configuration, creates clients, and starts a polling
/// loop for each device. Updates measures via gRPC.
/// </summary>
internal sealed class ModbusPollingManager : BackgroundService
{
    private readonly ModbusRunletOptions _options;
    private readonly IServiceDiscovery _discovery;
    private readonly ILogger<ModbusPollingManager> _logger;
    private readonly Lock _clientsLock = new();
    private readonly List<IModbusClient> _clients = [];

    public ModbusPollingManager(
        ModbusRunletOptions options,
        IServiceDiscovery discovery,
        ILogger<ModbusPollingManager> logger)
    {
        _options = options;
        _discovery = discovery;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ModbusConfig config;
        try
        {
            var parser = new ModbusConfigParser(_logger);
            var configPath = _options.ConfigPath
                ?? Environment.GetEnvironmentVariable("TINKWELL_CONFIG");
            if (configPath is null)
            {
                _logger.LogWarning("No Modbus configuration path specified — nothing to poll");
                return;
            }

            config = await parser.LoadFileAsync(configPath, cancellationToken: stoppingToken);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Modbus configuration");
            return;
        }

        if (config.Connections.Count == 0)
        {
            _logger.LogInformation("No modbus blocks found in configuration");
            return;
        }

        MeasuresGrpc.Measures.MeasuresClient? measuresClient = null;
        for (int attempt=0; attempt < 30 && !stoppingToken.IsCancellationRequested; ++attempt)
        {
            try
            {
                var svc = await _discovery.DiscoverAsync("measures", stoppingToken);
                if (svc is not null)
                {
                    measuresClient = await _discovery
                        .CreateInstanceAsync<MeasuresGrpc.Measures.MeasuresClient>(svc, stoppingToken);
                    break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Measures service discovery attempt {Attempt} failed", attempt + 1);
            }
            await Task.Delay(1000, stoppingToken);
        }

        if (measuresClient is null)
        {
            _logger.LogError("Could not discover measures service — Modbus polling disabled");
            return;
        }

        var tasks = new List<Task>();
        foreach (var conn in config.Connections)
        {
            foreach (var device in conn.Devices)
            {
                tasks.Add(PollDeviceAsync(conn, device, measuresClient, stoppingToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task PollDeviceAsync(
        ModbusConnectionDefinition conn,
        ModbusDeviceDefinition device,
        MeasuresGrpc.Measures.MeasuresClient measuresClient,
        CancellationToken ct)
    {
        IModbusClient client;
        try
        {
            client = CreateClient(conn);
            lock (_clientsLock) { _clients.Add(client); }
            await client.ConnectAsync(ct);
            _logger.LogInformation(
                "Connected to Modbus {Transport} device {SlaveId} on {Connection}",
                conn.Transport, device.SlaveId, conn.Transport == ModbusTransport.Tcp ? conn.Host : conn.Port);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Modbus device {SlaveId} on {Connection}",
                device.SlaveId, conn.Name);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            foreach (var reg in device.Registers)
            {
                try
                {
                    var count = (ushort)RegisterDecoder.RegisterCount(reg.DataType);
                    var registers = reg.RegisterKind == ModbusRegisterKind.Input
                        ? await client.ReadInputRegistersAsync(device.SlaveId, reg.Address, count, ct)
                        : await client.ReadHoldingRegistersAsync(device.SlaveId, reg.Address, count, ct);

                    var value = RegisterDecoder.Decode(registers, reg.DataType, reg.Scale);

                    await measuresClient.UpdateAsync(new MeasuresGrpc.UpdateMeasureRequest
                    {
                        Name = reg.MeasureName,
                        Value = new MeasuresGrpc.MeasureValueProto
                        {
                            Type = "number",
                            NumericValue = value,
                        },
                    }, cancellationToken: ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling register {Register} on device {SlaveId}",
                        reg.Name, device.SlaveId);
                }
            }

            try
            {
                await Task.Delay(device.PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static IModbusClient CreateClient(ModbusConnectionDefinition conn) =>
        conn.Transport switch
        {
            ModbusTransport.Tcp => new ModbusTcpClient(conn.Host ?? "localhost", conn.TcpPort),
            ModbusTransport.Rtu => new ModbusRtuClient(conn.Port ?? throw new InvalidOperationException(
                $"Modbus RTU connection '{conn.Name}' requires a 'port' property."), conn.BaudRate),
            _ => throw new InvalidOperationException($"Unknown transport: {conn.Transport}"),
        };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        List<IModbusClient> snapshot;
        lock (_clientsLock) { snapshot = [.. _clients]; }
        foreach (var client in snapshot)
            await client.DisposeAsync();
    }
}