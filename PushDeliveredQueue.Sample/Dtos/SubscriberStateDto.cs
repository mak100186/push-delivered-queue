namespace PushDeliveredQueue.Sample.Dtos;

public class SubscriberStateDto
{
    public bool IsBlocked { get; set; }
    public int PendingMessageCount { get; set; }
    public List<DeadLetterMessagesDto> DeadLetterQueue { get; set; } = new();
}
