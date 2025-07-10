using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Tinkwell.Bridge.MqttClient;

sealed class MqttClientBridge : IAsyncDisposable
{
    public MqttClientBridge(ILogger<MqttClientBridge> logger, ServiceLocator locator, MqttMessageParser messageParser, MqttBridgeOptions options)
    {
        _logger = logger;
        _locator = locator;
        _messageParser = messageParser;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false) == true)
            throw new ObjectDisposedException(nameof(MqttClientBridge));

        _logger.LogDebug("Starting MQTT client");
        _store = await _locator.FindStoreAsync(cancellationToken);

        CreateMqttClient();
        await ConnectAsync(cancellationToken);

        _logger.LogInformation("MQTT client started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false) == true)
            return;

        try
        {
            if (_mqttClient is not null)
            {
                _mqttClient.ConnectedAsync -= HandleClientConnectedAsync;
                _mqttClient.DisconnectedAsync -= HandleClientDissconnectedAsync;
                _mqttClient.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceivedAsync;
                if (_mqttClient.IsConnected)
                {
                    try
                    {
                        await _mqttClient.DisconnectAsync();
                    }
                    catch (Exception)
                    {
                    }
                }
                _mqttClient.Dispose();
            }

            if (_locator is not null)
                await _locator.DisposeAsync();

            if (_store is not null)
                await _store.DisposeAsync();
        }
        finally
        {
            _mqttClient = null;
            _store = null;
            _disposed = true;
        }
    }

    private readonly ILogger<MqttClientBridge> _logger;
    private readonly ServiceLocator _locator;
    private readonly MqttBridgeOptions _options;
    private readonly MqttMessageParser _messageParser;
    private GrpcService<Services.Store.StoreClient>? _store;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _clientOptions;
    private readonly ConcurrentDictionary<string, bool> _unregisteredMeasures = new();
    private bool _disposed;

    private void CreateMqttClient()
    {
        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerAddress, _options.BrokerPort)
            .WithCleanSession();

        if (_options.UseCredentials)
        {
            clientOptionsBuilder.WithCredentials(
                Environment.ExpandEnvironmentVariables(_options.Username!),
                Environment.ExpandEnvironmentVariables(_options.Password ?? "")
            );
        }

        _clientOptions = clientOptionsBuilder.Build();

        _mqttClient.ConnectedAsync += HandleClientConnectedAsync;
        _mqttClient.DisconnectedAsync += HandleClientDissconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
    }

    private async Task HandleClientConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogDebug("MQTT client connected to {Server}:{Port}",
            _options.BrokerAddress, _options.BrokerPort);

        var topicFilter = new MqttTopicFilterBuilder()
            .WithTopic(_options.TopicFilter)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build();

        await _mqttClient.SubscribeAsync(topicFilter);
        _logger.LogDebug("Subscribed to MQTT topic: {TopicFilter}", _options.TopicFilter);
    }

    private async Task HandleClientDissconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Debug.Assert(_mqttClient is not null);
        Debug.Assert(_clientOptions is not null);

        if (CanReconnect())
        {
            _logger.LogWarning("MQTT client disconnected from broker. Reason: {Reason}", e.Reason);
            await ConnectAsync(CancellationToken.None);
        }
        else
        {
            _logger.LogError("MQTT client disconnected from broker. Reason: {Reason}. Reconnection is not allowed.",
                e.Reason);
        }

        bool CanReconnect()
        {
            return e.Reason switch
            {
                MqttClientDisconnectReason.SessionTakenOver => true,
                MqttClientDisconnectReason.ImplementationSpecificError => true,
                MqttClientDisconnectReason.MaximumConnectTime => true,
                MqttClientDisconnectReason.MessageRateTooHigh => true,
                MqttClientDisconnectReason.QuotaExceeded => true,
                MqttClientDisconnectReason.ReceiveMaximumExceeded => true,
                MqttClientDisconnectReason.ServerBusy => true,
                MqttClientDisconnectReason.UnspecifiedError => true,
                _ => false
            };
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_mqttClient is not null);
        Debug.Assert(_clientOptions is not null);

        for (int i = 0; i < _options.NumberOfRetriesOnError; ++i)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            _logger.LogTrace("Connecting to MQTT broker (attempt {Attempt}/{TotalAttempts})...",
                i + 1, _options.NumberOfRetriesOnError);

            try
            {
                await _mqttClient.ConnectAsync(_clientOptions, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                if (i < _options.NumberOfRetriesOnError - 1)
                {
                    _logger.LogWarning(e, "Failed to connect to MQTT broker. Retrying in {Delay}ms...",
                        _options.RetryDelayInMilliseconds);

                    await Task.Delay(_options.RetryDelayInMilliseconds, cancellationToken);
                }
                else
                    _logger.LogError(e, "Failed to connect to MQTT broker: {Reason}", e.Message);
            }
        }
    }

    private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        Debug.Assert(_store is not null);

        var topic = arg.ApplicationMessage.Topic;
        var payload = arg.ApplicationMessage.ConvertPayloadToString();
        _logger.LogTrace("Received MQTT message on topic '{Topic}': {Payload}", topic, payload);

        var measures = ExtractMeasures(topic, payload);
        if (measures is null)
            return;

        foreach (var measure in measures.Where(x => !_unregisteredMeasures.ContainsKey(x.Name)))
        {
            try
            {
                await _store.Client.SetAsync(new Services.StoreSetRequest
                {
                    Name = measure.Name,
                    Value = (double)measure.Value // TODO: support text strings with unit of measure!!!
                });
                _logger.LogTrace("Updated measure '{MeasureName}' with value {Value}.", measure.Name, measure.Value);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Measure '{MeasureName}' not found in Store. We will NOT try again in future.", measure.Name);
                _unregisteredMeasures.TryAdd(measure.Name, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update measure '{MeasureName}' in Store.", measure.Name);
            }
        }
    }

    private IEnumerable<MqttMeasure>? ExtractMeasures(string topic, string payload)
    {
        try
        {
            return _messageParser.Parse(topic, payload);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to parse MQTT message on topic '{Topic}' with payload '{Payload}'. Reason: {Reason}",
                topic, payload, e.Message);
        }

        return null;
    }
}
