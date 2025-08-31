namespace PushDeliveredQueue.Core;

public partial class SubscribableQueue : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_cts != null)
        {
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch (Exception)
            {
                // Already disposed, ignore
            }
        }

        foreach (var sub in _subscribers.Values)
        {
            try
            {
                sub.Cancellation.Cancel();
                sub.Cancellation.Dispose();
            }
            catch (Exception)
            {
                // Already disposed, ignore
            }
        }
    }
}
