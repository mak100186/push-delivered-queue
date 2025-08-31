using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core.Abstractions;

public interface IQueueEventHandler
{
    Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken);
    Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId, Exception? exception, CancellationToken cancellationToken);
    Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken);
}
