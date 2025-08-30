namespace PushDeliveredQueue.Core.Models;

public class SubscribableQueueState
{
    public TimeSpan Ttl { get; set; }
    public List<MessageEnvelope> Buffer { get; set; } = new();
    public Dictionary<Guid, SubscriberState> Subscribers { get; set; } = new();
}
