using PushDeliveredQueue.Core.Abstractions;

namespace PushDeliveredQueue.Core.Models;

public class CursorState
{
    public int Index { get; set; }
    public bool IsCommitted { get; set; }
    public required IQueueEventHandler Handler { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();
    public List<MessageEnvelope> DeadLetterQueue = [];
}
