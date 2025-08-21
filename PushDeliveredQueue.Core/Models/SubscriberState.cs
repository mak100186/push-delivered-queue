namespace PushDeliveredQueue.Core.Models;

public class SubscriberState
{
    public int CursorIndex { get; set; }
    public bool IsCommitted { get; set; }

    public int PendingCount { get; set; }
}