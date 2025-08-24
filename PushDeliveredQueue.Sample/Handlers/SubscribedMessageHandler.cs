using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Sample.Handlers;

public class SubscribedMessageHandler(ILogger<SubscribedMessageHandler> logger)
{
    public async Task<DeliveryResult> HandlerAsync(MessageEnvelope msg)
    {
        logger.LogInformation("Processing: MessageId:[{MessageId}] Payload:[{Payload}]", msg.Id, msg.Payload);
        if (msg.Payload.Contains("fail"))
        {
            return DeliveryResult.Nack;
        }
        await Task.Delay(10); // Simulate work

        logger.LogInformation("Processed: MessageId:[{MessageId}] Payload:[{Payload}]", msg.Id, msg.Payload);
        return DeliveryResult.Ack;
    }
}
