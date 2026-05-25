using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Tinkwell.Runlet.Mqtt.Configuration;
using Tinkwell.Expressions;
using Tinkwell.Integration;
using Tinkwell.Telemetry;

namespace Tinkwell.Runlet.Mqtt;

/// <summary>
/// Manages the lifecycle of a single MQTT broker connection: connect,
/// subscribe to topics, receive messages, and run the binding chain per subscription.
/// Handles reconnection on disconnect.
/// </summary>
internal sealed class MqttConnectionWorker
{
    private readonly MqttConnectionDefinition _connection;
    private readonly MqttBindingChainExecutor _executor;
    private readonly IReadOnlyList<IMqttMiddleware> _middlewares;
    private readonly IExpressionEvaluator _evaluator;
    private readonly ILogger<MqttConnectionWorker> _logger;
    private readonly Channel<MqttIngressMessage> _ingressChannel;

    private readonly List<TopicSubscription> _subscriptions = [];

    private long _droppedMessages;

    /// <summary>
    /// Number of messages dropped because the ingress channel was full.
    /// </summary>
    public long DroppedMessages => Interlocked.Read(ref _droppedMessages);

    public MqttConnectionWorker(
        MqttConnectionDefinition connection,
        MqttBindingChainExecutor executor,
        IReadOnlyList<IMqttMiddleware> middlewares,
        IExpressionEvaluator evaluator,
        ILogger<MqttConnectionWorker> logger)
    {
        _connection = connection;
        _executor = executor;
        _middlewares = middlewares;
        _evaluator = evaluator;
        _logger = logger;

        _ingressChannel = connection.MaxPendingMessages > 0
            ? Channel.CreateBounded<MqttIngressMessage>(
                new BoundedChannelOptions(connection.MaxPendingMessages)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false,
                })
            : Channel.CreateUnbounded<MqttIngressMessage>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        foreach (var sub in connection.Subscriptions)
            _subscriptions.Add(new TopicSubscription(sub));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        var options = BuildClientOptions();

        client.ApplicationMessageReceivedAsync += args =>
        {
            var mqttUserProps = args.ApplicationMessage.UserProperties;
            IReadOnlyList<MessageProperty>? userProperties = mqttUserProps is null or { Count: 0 }
                ? null
                : mqttUserProps.Select(p => new MessageProperty(p.Name, p.ReadValueAsString())).ToList();

            var msg = new MqttIngressMessage(
                args.ApplicationMessage.Topic,
                args.ApplicationMessage.ConvertPayloadToString(),
                userProperties);

            if (!_ingressChannel.Writer.TryWrite(msg))
            {
                Interlocked.Increment(ref _droppedMessages);
                _logger.LogWarning(
                    "MQTT '{Name}' ingress channel full, dropping message on topic '{Topic}'",
                    _connection.Name, msg.Topic);
            }

            return Task.CompletedTask;
        };

        client.ConnectedAsync += async _ =>
        {
            _logger.LogInformation("MQTT '{Name}' connected to {Broker}:{Port}",
                _connection.Name, _connection.Broker, _connection.Port);

            var topicFilters = _connection.Subscriptions
                .Select(s => new MqttTopicFilterBuilder()
                    .WithTopic(s.TopicFilter)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build())
                .ToList();

            await client.SubscribeAsync(
                new MqttClientSubscribeOptions { TopicFilters = topicFilters },
                ct);

            _logger.LogDebug("MQTT '{Name}' subscribed to {Count} topic(s)",
                _connection.Name, topicFilters.Count);
        };

        var consumerTask = ConsumeIngressAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectWithRetryAsync(client, options, ct);
                await WaitForDisconnectOrCancellationAsync(client, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "MQTT '{Name}' disconnected unexpectedly, reconnecting in {Delay}ms",
                    _connection.Name, _connection.RetryDelay);

                try { await Task.Delay(_connection.RetryDelay, ct); }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _ingressChannel.Writer.TryComplete();
        await consumerTask;

        if (client.IsConnected)
        {
            try
            {
                await client.DisconnectAsync(new MqttClientDisconnectOptions(),
                    CancellationToken.None);
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to disconnect MQTT client on shutdown");
            }
        }

        _logger.LogInformation("MQTT '{Name}' worker stopped", _connection.Name);
    }

    private async Task ConsumeIngressAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var msg in _ingressChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await HandleMessageAsync(msg.Topic, msg.Payload, msg.UserProperties, ct);
                }
                catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error handling MQTT message on topic '{Topic}'",
                        msg.Topic);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_connection.ClientId)
            .WithTcpServer(_connection.Broker, _connection.Port);

        if (_connection.Username is not null)
        {
            builder.WithCredentials(
                Environment.ExpandEnvironmentVariables(_connection.Username),
                Environment.ExpandEnvironmentVariables(_connection.Password ?? ""));
        }

        return builder.Build();
    }

    private async Task ConnectWithRetryAsync(
        IMqttClient client, MqttClientOptions options, CancellationToken ct)
    {
        using var span = OtTraces.Source.Timed(
            OtTraces.Connect, OtMetrics.ConnectDuration,
            (OtTraces.ConnectionName, _connection.Name));

        for (int attempt=1; attempt <= _connection.RetryCount; ++attempt)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "MQTT '{Name}' connecting to {Broker}:{Port} (attempt {Attempt}/{Total})",
                _connection.Name, _connection.Broker, _connection.Port,
                attempt, _connection.RetryCount);

            try
            {
                await client.ConnectAsync(options, ct);
                OtMetrics.ConnectAttempts.Add(1,
                    new KeyValuePair<string, object?>(OtTraces.ConnectionName, _connection.Name),
                    new KeyValuePair<string, object?>(OtTraces.ConnectResult, "success"));
                span.SetTag(OtTraces.ConnectResult, "success");
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
            catch (Exception ex)
            {
                OtMetrics.ConnectAttempts.Add(1,
                    new KeyValuePair<string, object?>(OtTraces.ConnectionName, _connection.Name),
                    new KeyValuePair<string, object?>(OtTraces.ConnectResult, "error"));

                if (attempt < _connection.RetryCount)
                {
                    _logger.LogWarning(
                        "MQTT '{Name}' connection attempt {Attempt} failed: {Reason}. Retrying in {Delay}ms",
                        _connection.Name, attempt, ex.Message, _connection.RetryDelay);
                    await Task.Delay(_connection.RetryDelay, ct);
                }
                else
                {
                    span.Error($"failed after {_connection.RetryCount} attempts");
                    _logger.LogError(ex,
                        "MQTT '{Name}' failed to connect after {Count} attempts",
                        _connection.Name, _connection.RetryCount);
                    throw;
                }
            }
        }
    }

    private static async Task WaitForDisconnectOrCancellationAsync(
        IMqttClient client, CancellationToken ct)
    {
        var disconnectTcs = new TaskCompletionSource();
        Func<MqttClientDisconnectedEventArgs, Task> handler = _ =>
        {
            disconnectTcs.TrySetResult();
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += handler;
        try
        {
            using var reg = ct.Register(() => disconnectTcs.TrySetCanceled(ct));
            await disconnectTcs.Task;
        }
        finally
        {
            client.DisconnectedAsync -= handler;
        }
    }

    private async Task HandleMessageAsync(
        string topic, string payload,
        IReadOnlyList<MessageProperty>? userProperties, CancellationToken ct)
    {
        _logger.LogTrace("MQTT '{Name}' received message on '{Topic}': {Payload}",
            _connection.Name, topic, payload);

        if (_middlewares.Count > 0)
        {
            var context = new MqttMessageContext
            {
                Topic = topic,
                Payload = payload,
                ConnectionName = _connection.Name,
                UserProperties = userProperties,
            };

            await RunPipeline(context, ct);
        }
        else
        {
            await ExecuteBindingsAsync(topic, payload, ct);
        }
    }

    private Task RunPipeline(MqttMessageContext context, CancellationToken ct)
    {
        Func<MqttMessageContext, CancellationToken, Task> pipeline =
            (ctx, token) => ExecuteBindingsAsync(ctx.Topic, ctx.Payload, token);

        for (int i=_middlewares.Count - 1; i >= 0; --i)
        {
            var mw = _middlewares[i];
            var next = pipeline;
            pipeline = (ctx, token) => mw.InvokeAsync(ctx, next, token);
        }

        return pipeline(context, ct);
    }

    private async Task ExecuteBindingsAsync(string topic, string payload, CancellationToken ct)
    {
        foreach (var sub in _subscriptions)
        {
            if (!sub.Matches(topic))
                continue;

            await _executor.ExecuteAsync(sub.Definition, topic, payload, ct);

            _logger.LogDebug(
                "MQTT '{Name}' executed binding chain for topic '{Topic}'",
                _connection.Name, topic);
        }
    }

    /// <summary>
    /// Wraps a subscription definition with a compiled topic filter matcher.
    /// </summary>
    private sealed class TopicSubscription
    {
        public MqttSubscriptionDefinition Definition { get; }

        private readonly string[] _filterSegments;
        private readonly bool _hasMultiLevelWildcard;

        public TopicSubscription(MqttSubscriptionDefinition definition)
        {
            Definition = definition;
            _filterSegments = definition.TopicFilter.Split('/');
            _hasMultiLevelWildcard = _filterSegments[^1] == "#";
        }

        public bool Matches(string topic)
        {
            var topicSegments = topic.Split('/');

            if (_hasMultiLevelWildcard)
            {
                if (topicSegments.Length < _filterSegments.Length - 1)
                    return false;

                for (int i=0; i < _filterSegments.Length - 1; ++i)
                {
                    if (_filterSegments[i] == "+")
                        continue;
                    if (!string.Equals(_filterSegments[i], topicSegments[i], StringComparison.Ordinal))
                        return false;
                }

                return true;
            }

            if (topicSegments.Length != _filterSegments.Length)
                return false;

            for (int i=0; i < _filterSegments.Length; ++i)
            {
                if (_filterSegments[i] == "+")
                    continue;
                if (!string.Equals(_filterSegments[i], topicSegments[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }

    private readonly record struct MqttIngressMessage(
        string Topic,
        string Payload,
        IReadOnlyList<MessageProperty>? UserProperties);
}