using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core.Abstractions;
public interface IQueueEventHandler
{
    Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId);
    Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId);
    Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId);
}
