using Grpc.Core;
using Microsoft.Extensions.Logging;
using Tinkwell.Services;

namespace Tinkwell.Bridge.MqttClient.Services;

sealed class MqttClientService : Tinkwell.Services.MqttClient.MqttClientBase
{
    public MqttClientService(ILogger<MqttClientService> logger, MqttClientBridge bridge)
    {
        _logger = logger;
        _bridge = bridge;
    }

    public override async Task<PublishMqttMessageResponse> Publish(PublishMqttMessageRequest request, ServerCallContext context)
    {
        try
        {
            var result = await _bridge.PublishAsync(
                request.Topic, request.Payload, context.CancellationToken);

            return new PublishMqttMessageResponse
            {
                Status = result switch
                {
                    PublishMessageResult.Ok => PublishMqttMessageResponse.Types.Status.Ok,
                    PublishMessageResult.NoSubscribers => PublishMqttMessageResponse.Types.Status.NoSubscribers,
                    _ => PublishMqttMessageResponse.Types.Status.Error,
                }
            };
        }
        catch (ArgumentException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message, e));
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to publish MQTT message: {Message}", e.Message);
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to publish MQTT message: {e.Message}", e));
        }
    }

    private readonly ILogger<MqttClientService> _logger;
    private readonly MqttClientBridge _bridge;
}