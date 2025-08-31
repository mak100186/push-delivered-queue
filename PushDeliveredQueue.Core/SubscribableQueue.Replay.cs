using Microsoft.Extensions.Logging;

using PushDeliveredQueue.Core.Models;

using static PushDeliveredQueue.Core.Constants.SubscribableQueueConstants;

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
                _logger.LogInformation(LogMessagePayloadChanged, messageId);
            }
            else
            {
                _logger.LogWarning(LogMessageNotFoundForPayloadChange, messageId);
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
                _logger.LogWarning(LogMessageNotFoundInDlqForReplay, messageId, subscriberId);
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
            _logger.LogWarning(LogNonExistentSubscriberForDlqReplay, subscriberId);
        }
    }

    private async Task<bool> ProcessMessageFromDlqAsync(Guid subscriberId, CursorState cursor, MessageEnvelope message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(LogSubscriberReplayedDlqMessage, message.Id, subscriberId);

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
            _logger.LogInformation(LogSubscriberProcessedDlqMessage, subscriberId, message.Id);

            return true;
        }
        else
        {
            await cursor.Handler.OnMessageFailedHandlerAsync(message, subscriberId, exception, cancellationToken);

            _logger.LogInformation(LogSubscriberDlqProcessingFailed, subscriberId, message);

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
                _logger.LogInformation(LogSubscriberReplayedAllDlqMessages, subscriberId);
            }
            else
            {
                _logger.LogInformation(LogSubscriberNoDlqMessagesToReplay, subscriberId);
            }
        }
        else
        {
            _logger.LogWarning(LogNonExistentSubscriberForDlqReplay, subscriberId);
        }
    }

    public void ReplayAllDlqSubscribers(CancellationToken cancellationToken)
    {
        foreach (var subscriberId in _subscribers.Keys)
        {
            Task.Run(async () => await ReplayAllDlqMessagesAsync(subscriberId, cancellationToken), cancellationToken);
        }
    }

    public void ReplayFrom(Guid subscriberId, Guid messageId)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            if (cursor.Index < 0)
            {
                _logger.LogWarning(LogSubscriberNotStartedConsuming, subscriberId);
                return;
            }

            if (!cursor.IsCommitted)
            {
                _logger.LogWarning(LogSubscriberHasUncommittedMessages, subscriberId);
                return;
            }

            if (cursor.Index + 1 < _buffer.Count)
            {
                _logger.LogWarning(LogSubscriberAtMiddleOfBuffer, subscriberId);
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
                _logger.LogInformation(LogSubscriberReplayedFromMessage, subscriberId, messageId, index);
            }
            else
            {
                _logger.LogWarning(LogMessageNotFoundInBufferForReplay, messageId, subscriberId);
            }
        }
        else
        {
            _logger.LogWarning(LogNonExistentSubscriberForReplay, subscriberId);
        }
    }
}
