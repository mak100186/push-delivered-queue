namespace PushDeliveredQueue.API.Dtos;

public class SubscribableQueueStateDto
{
    public List<MessageEnvelopeDto> Buffer { get; set; } = new();
    public Dictionary<Guid, SubscriberStateDto> Subscribers { get; set; } = new();
}
