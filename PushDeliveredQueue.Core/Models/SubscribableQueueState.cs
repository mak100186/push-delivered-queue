namespace PushDeliveredQueue.Core.Models;

public class SubscribableQueueState
{
    public List<MessageEnvelope> Buffer { get; set; } = new();
    public Dictionary<Guid, SubscriberState> Subscribers { get; set; } = new();
}