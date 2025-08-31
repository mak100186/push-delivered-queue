using Microsoft.Extensions.Logging;

using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core;

public partial class SubscribableQueue
{
    public void ChangeMessagePayload(Guid messageId, string payload, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var message = _buffer.FirstOrDefault(m => m.Id == messageId);

            if (message != null)
            {
                message.ChangePayload(payload);
                _logger.LogInformation("Message {MessageId} payload changed", messageId);
            }
            else
            {
                _logger.LogWarning("Message {MessageId} not found for payload change", messageId);
            }
        }
    }
    public async Task ReplayFromDlqAsync(Guid subscriberId, Guid messageId, CancellationToken cancellationToken)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            var messageFromDlq = cursor.DeadLetterQueue.FirstOrDefault(m => m.Id == messageId);

            if (messageFromDlq == null)
            {
                _logger.LogWarning("Message {MessageId} not found in DLQ for replay by subscriber {SubscriberId}", messageId, subscriberId);
                return;
            }

            var success = await ProcessMessageFromDlqAsync(subscriberId, cursor, messageFromDlq, cancellationToken);

            if (success)
            {
                cursor.DeadLetterQueue.RemoveAll(m => m.Id == messageFromDlq.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to replay DLQ for non-existent subscriber {SubscriberId}", subscriberId);
        }
    }

    private async Task<bool> ProcessMessageFromDlqAsync(Guid subscriberId, CursorState cursor, MessageEnvelope message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Replaying message {MessageId} to subscriber {SubscriberId} from dead letter queue", message.Id, subscriberId);

        var result = DeliveryResult.Nack;
        Exception? exception = null;

        try
        {
            result = await cursor.Handler.OnMessageReceiveAsync(message, subscriberId, cancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (result == DeliveryResult.Ack)
        {
            _logger.LogInformation("Subscriber {SubscriberId} successfully processed DLQ message {MessageId}", subscriberId, message.Id);

            return true;
        }
        else
        {
            await cursor.Handler.OnMessageFailedHandlerAsync(message, subscriberId, exception, cancellationToken);

            _logger.LogInformation("DLQ Message processing failed. Not removing from DLQ {SubscriberId} {@Message}", subscriberId, message);

            return false;
        }
    }

    public async Task ReplayAllDlqMessagesAsync(Guid subscriberId, CancellationToken cancellationToken)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            if (cursor.DeadLetterQueue.Count > 0)
            {
                for (var i = 0; i < cursor.DeadLetterQueue.Count; i++)
                {
                    var messageFromDlq = cursor.DeadLetterQueue[i];
                    var success = await ProcessMessageFromDlqAsync(subscriberId, cursor, messageFromDlq, cancellationToken);

                    if (success)
                    {
                        cursor.DeadLetterQueue.RemoveAll(m => m.Id == messageFromDlq.Id);
                        i--; // Adjust index since we removed an item
                    }
                }
                _logger.LogInformation("Subscriber {SubscriberId} replayed all DLQ messages", subscriberId);
            }
            else
            {
                _logger.LogInformation("Subscriber {SubscriberId} has no messages in DLQ to replay", subscriberId);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to replay DLQ for non-existent subscriber {SubscriberId}", subscriberId);
        }
    }

    public void ReplayAllDlqSubscribers(CancellationToken cancellationToken)
    {
        foreach (var subscriberId in _subscribers.Keys)
        {
            Task.Run(async () => await ReplayAllDlqMessagesAsync(subscriberId, cancellationToken));
        }
    }

    public void ReplayFrom(Guid subscriberId, Guid messageId)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            if (cursor.Index < 0)
            {
                _logger.LogWarning("Subscriber {SubscriberId} has not started consuming messages yet. Cannot replay.", subscriberId);
                return;
            }

            if (!cursor.IsCommitted)
            {
                _logger.LogWarning("Subscriber {SubscriberId} has uncommitted messages. Cannot replay.", subscriberId);
                return;
            }

            if (cursor.Index + 1 < _buffer.Count)
            {
                _logger.LogWarning("Subscriber {SubscriberId} is at the middle of the buffer. Cannot replay.", subscriberId);
                return;
            }

            var index = -1;
            lock (_lock)
            {
                index = _buffer.FindIndex(m => m.Id == messageId);
            }

            if (index >= 0)
            {
                cursor.Index = index;
                cursor.IsCommitted = false;
                _logger.LogInformation("Subscriber {SubscriberId} replayed from message {MessageId} at index {Index}", subscriberId, messageId, index);
            }
            else
            {
                _logger.LogWarning("Message {MessageId} not found in buffer for replay by subscriber {SubscriberId}", messageId, subscriberId);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to replay for non-existent subscriber {SubscriberId}", subscriberId);
        }
    }
}
