using Grpc.Core;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Bridge.MqttClient.Internal;
using Tinkwell.Services;

namespace Tinkwell.Bridge.MqttClient;

enum PublishMessageResult
{
    Ok,
    NoSubscribers,
    Error,
}

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
        if (Interlocked.CompareExchange(ref _disposed, true, true) == true)
            throw new ObjectDisposedException(nameof(MqttClientBridge));

        _logger.LogDebug("Starting MQTT client");

        if (!string.IsNullOrWhiteSpace(_options.Mapping))
            _messageParser.LoadMapping(HostingInformation.GetFullPath(_options.Mapping));
    
        _store = await _locator.FindStoreAsync(cancellationToken);
        await CreateMqttClientAndConnectAsync(cancellationToken);

        _logger.LogInformation("MQTT client started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await DisposeAsync();

    public async Task<PublishMessageResult> PublishAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (_mqttClient is null)
            throw new InvalidOperationException("MQTT client is not initialized. Please start the bridge first.");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        _logger.LogTrace("Publishing MQTT message on topic '{Topic}': {Payload}", topic, payload);
        var result = await _mqttClient.PublishAsync(message, cancellationToken);

        return result.ReasonCode switch
        {
            MqttClientPublishReasonCode.Success => PublishMessageResult.Ok,
            MqttClientPublishReasonCode.NoMatchingSubscribers => PublishMessageResult.NoSubscribers,
            _ => PublishMessageResult.Error
        };
    }

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

            if (_store is not null)
                await _store.DisposeAsync();
        }
        finally
        {
            _mqttClient = null;
            _store = null;
        }
    }

    private readonly ILogger<MqttClientBridge> _logger;
    private readonly ServiceLocator _locator;
    private readonly MqttBridgeOptions _options;
    private readonly MqttMessageParser _messageParser;
    private GrpcService<Tinkwell.Services.Store.StoreClient>? _store;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _clientOptions;
    private readonly ConcurrentBag<string> _unregisteredMeasures = new();
    private bool _disposed;

    private async Task CreateMqttClientAndConnectAsync(CancellationToken cancellationToken)
    {
        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerAddress, _options.BrokerPort);

        if (_options.UseCredentials)
        {
            clientOptionsBuilder.WithCredentials(
                Environment.ExpandEnvironmentVariables(_options.Username!),
                Environment.ExpandEnvironmentVariables(_options.Password ?? "")
            );
        }

        _clientOptions = clientOptionsBuilder.Build();

        _mqttClient.ConnectedAsync += HandleClientConnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;

        await ConnectAsync(cancellationToken);
        _mqttClient.DisconnectedAsync += HandleClientDissconnectedAsync;
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

        _logger.LogWarning("MQTT client disconnected from broker. Reason: {Reason}", e.Reason);
        if (CanReconnect())
        {
            await Task.Delay(_options.RetryDelayInMilliseconds, CancellationToken.None);
            await ConnectAsync(CancellationToken.None);
        }

        bool CanReconnect()
        {
            return e.Reason switch
            {
                MqttClientDisconnectReason.ConnectionRateExceeded => true,
                MqttClientDisconnectReason.ImplementationSpecificError => true,
                MqttClientDisconnectReason.KeepAliveTimeout => true,
                MqttClientDisconnectReason.MaximumConnectTime => true,
                MqttClientDisconnectReason.MessageRateTooHigh => true,
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

            _logger.LogDebug("Connecting to MQTT broker {Address}:{Port} (attempt {Attempt}/{TotalAttempts})...",
                _options.BrokerAddress, _options.BrokerPort, i + 1, _options.NumberOfRetriesOnError);

            try
            {
                await _mqttClient.ConnectAsync(_clientOptions, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                if (i < _options.NumberOfRetriesOnError - 1)
                {
                    _logger.LogWarning("Failed to connect to MQTT broker ({Reason)}. Retrying in {Delay}ms...",
                        e.Message, _options.RetryDelayInMilliseconds);

                    await Task.Delay(_options.RetryDelayInMilliseconds, cancellationToken);
                }
                else
                    _logger.LogError(e, "Failed to connect to MQTT broker: {Reason}", e.Message);
            }
        }
    }

    private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = arg.ApplicationMessage.ConvertPayloadToString();
        _logger.LogTrace("Received MQTT message on topic '{Topic}': {Payload}", topic, payload);

        foreach (var measure in ExtractMeasures(topic, payload))
            await WriteMeasureAsync(measure);
    }

    private IEnumerable<MqttMeasure> ExtractMeasures(string topic, string payload)
    {
        try
        {
            return _messageParser
                .Parse(topic, payload)
                .Where(x => !_unregisteredMeasures.Contains(x.Name, StringComparer.Ordinal))
                .ToList();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to parse MQTT message on topic '{Topic}' with payload '{Payload}'. Reason: {Reason}",
                topic, payload, e.Message);
        }

        return [];
    }

    private async Task WriteMeasureAsync(MqttMeasure measure)
    {
        Debug.Assert(_store is not null);

        try
        {
            var request = new StoreUpdateRequest();
            request.Name = measure.Name;
            request.Value.Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
            if (measure.IsNumeric)
                request.Value.NumberValue = measure.AsDouble();
            else
                request.Value.StringValue = measure.AsString();

            await _store.Client.UpdateAsync(request);
            _logger.LogTrace("Updated measure '{MeasureName}' with value {Value}.", measure.Name, measure.Value);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Measure '{MeasureName}' not found in Store. We will NOT try again in future.", measure.Name);
            _unregisteredMeasures.Add(measure.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update measure '{MeasureName}' in Store.", measure.Name);
        }
    }
}
