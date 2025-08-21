namespace PushDeliveredQueue.Core.Models;

public class CursorState
{
    public int Index { get; set; } = 0;
    public bool IsCommitted { get; set; } = false;
    public MessageHandler? Handler { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();
}
