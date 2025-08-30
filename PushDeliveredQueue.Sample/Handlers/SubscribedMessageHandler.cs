using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Sample.Handlers;

public class SubscribedMessageHandler(ILogger<SubscribedMessageHandler> logger) : IQueueEventHandler
{
    public async Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope msg, Guid subscriberId)
    {
        logger.LogInformation("Processing: SubscriberId:[{SubscriberId}] MessageId:[{MessageId}] Payload:[{Payload}]", subscriberId, msg.Id, msg.Payload);
        if (msg.Payload.Contains("fail"))
        {
            return DeliveryResult.Nack;
        }
        await Task.Delay(10); // Simulate work

        return DeliveryResult.Ack;
    }

    public Task<PostMessageFailedBehavior> OnMessageFailedHandler(MessageEnvelope message, Guid subscriberId)
    {
        logger.LogWarning("Failed: SubscriberId:[{SubscriberId}] MessageId:[{MessageId}] Payload:[{Payload}]", subscriberId, message.Id, message.Payload);

        return Task.FromResult(PostMessageFailedBehavior.Commit);
    }
}
