using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace Tinkwell.Studio.Services;

public sealed record MqttIncomingMessage(
    DateTimeOffset Timestamp,
    string Topic,
    byte[] Payload,
    int Qos,
    bool Retain)
{
    public string PayloadText => PayloadAsText();

    public string PayloadAsText(int maxLength = 4096)
    {
        if (Payload.Length == 0)
            return string.Empty;
        var bytes = Payload.Length <= maxLength ? Payload : Payload.AsSpan(0, maxLength).ToArray();
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return Convert.ToHexString(bytes);
        }
    }

    public IReadOnlyList<Detail> Details => new List<Detail>
    {
        new("Topic", Topic, DetailKind.Url),
        new("Time", Timestamp.ToLocalTime().ToString("u")),
        new("QoS", Qos.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Retain", Retain ? "true" : "false"),
        new("Payload length", $"{Payload.Length} byte(s)"),
    };
}

public sealed record MqttConnectionOptions(
    string Host,
    int Port,
    string? ClientId,
    string? Username,
    string? Password);

public sealed class MqttMonitorService : IAsyncDisposable
{
    private readonly ILogger<MqttMonitorService> _logger;
    private readonly ConcurrentDictionary<string, byte> _topics = new();
    private IMqttClient? _client;
    private MqttConnectionOptions? _options;

    public MqttMonitorService(ILogger<MqttMonitorService> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    public event EventHandler<MqttIncomingMessage>? MessageReceived;

    public event EventHandler<bool>? ConnectionChanged;

    public async Task ConnectAsync(MqttConnectionOptions options, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        var builder = factory.CreateClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithClientId(options.ClientId ?? $"tinkwell-studio-{Guid.NewGuid():N}"[..22]);

        if (!string.IsNullOrEmpty(options.Username))
            builder = builder.WithCredentials(options.Username, options.Password ?? string.Empty);

        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = e.ApplicationMessage.Payload.ToArray();
            var message = new MqttIncomingMessage(
                DateTimeOffset.UtcNow,
                e.ApplicationMessage.Topic,
                payload,
                (int)e.ApplicationMessage.QualityOfServiceLevel,
                e.ApplicationMessage.Retain);
            MessageReceived?.Invoke(this, message);
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += _ =>
        {
            ConnectionChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        _client = client;
        _options = options;
        ConnectionChanged?.Invoke(this, true);

        foreach (var topic in _topics.Keys)
            await SubscribeInternalAsync(topic, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (_client is null)
            return;

        try
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync().ConfigureAwait(false);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MQTT disconnect failed");
        }
        finally
        {
            _client.Dispose();
            _client = null;
            ConnectionChanged?.Invoke(this, false);
        }
    }

    public async Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        _topics[topicFilter] = 0;
        if (_client is { IsConnected: true })
            await SubscribeInternalAsync(topicFilter, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        _topics.TryRemove(topicFilter, out _);
        if (_client is { IsConnected: true })
            await _client.UnsubscribeAsync(topicFilter, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyCollection<string> SubscribedTopics => _topics.Keys.ToArray();

    public MqttConnectionOptions? CurrentOptions => _options;

    private async Task SubscribeInternalAsync(string topicFilter, CancellationToken cancellationToken)
    {
        if (_client is null)
            return;

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topicFilter).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();

        await _client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
        => await DisconnectAsync().ConfigureAwait(false);
}
