using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core.Abstractions;
public interface IQueueEventHandler
{
    Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId);
    Task<PostMessageFailedBehavior> OnMessageFailedHandler(MessageEnvelope message, Guid subscriberId);
}

public enum PostMessageFailedBehavior
{
    RetryOnceThenCommit, // will retry once immediately and then commit the message
    Commit,
    Block
}
