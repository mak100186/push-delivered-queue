using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.API.Handlers;

public class SubscribedMessageHandler(ILogger<SubscribedMessageHandler> logger) : IQueueEventHandler
{
    public async Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope msg, Guid subscriberId, CancellationToken cancellationToken)
    {
        logger.LogInformation("OnMessageReceiveAsync: SubscriberId:[{SubscriberId}] MessageId:[{MessageId}] Payload:[{Payload}]", subscriberId, msg.Id, msg.Payload);

        if (msg.Payload.Contains("fail"))
        {
            return DeliveryResult.Nack;
        }
        await Task.Delay(1000, cancellationToken); // Simulate work

        return DeliveryResult.Ack;
    }

    public Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId, Exception? exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Error processing message: SubscriberId:[{SubscriberId}] MessageId:[{MessageId}] Payload:[{Payload}]", subscriberId, message.Id, message.Payload);

        return Task.FromResult(PostMessageFailedBehavior.RetryOnceThenDLQ);
    }

    public Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken)
    {
        logger.LogWarning("OnDeadLetterHandlerAsync: SubscriberId:[{SubscriberId}] MessageId:[{MessageId}] Payload:[{Payload}]", subscriberId, message.Id, message.Payload);

        return Task.FromResult(DeliveryResult.Ack);
    }
}
