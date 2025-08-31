using Microsoft.Extensions.Logging;

namespace PushDeliveredQueue.Core;

public partial class SubscribableQueue
{
    private void PruneExpiredMessages(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTime.UtcNow - _ttl;

                lock (_lock)
                {
                    var removedCount = 0;

                    while (_buffer.Count > 0 && _buffer[0].CreatedAt < cutoff)
                    {
                        _buffer.RemoveAt(0);
                        removedCount++;
                    }

                    if (removedCount > 0)
                    {
                        foreach (var cursor in _subscribers.Values)
                        {
                            cursor.Index = Math.Max(0, cursor.Index - removedCount);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful exit
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pruning expired messages.");
            }
        }
    }
}
