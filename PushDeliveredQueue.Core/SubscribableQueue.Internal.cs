using Microsoft.Extensions.Logging;

using Polly;

using PushDeliveredQueue.Core.Models;

using static PushDeliveredQueue.Core.Constants.SubscribableQueueConstants;

namespace PushDeliveredQueue.Core;

public partial class SubscribableQueue
{
    private async Task OnFailedAsync(DelegateResult<DeliveryResult> result, Context context)
    {
        var subscriberId = Guid.Parse(context[ContextItemSubscriberId]?.ToString()!);
        var cursor = (CursorState)context[ContextItemCursorState]!;
        var message = (MessageEnvelope)context[ContextItemMessage]!;

        _logger.LogInformation("Message delivery failed. Invoking OnMessageFailedHandler. {SubscriberId} {@Message}", subscriberId, message);

        var postMessageFailedBehavior = await cursor.Handler.OnMessageFailedHandlerAsync(message, subscriberId, result.Exception, _cts.Token);

        _logger.LogInformation("OnMessageFailedHandler returned {PostMessageFailedBehavior} for {SubscriberId} {@Message}", postMessageFailedBehavior, subscriberId, message);

        switch (postMessageFailedBehavior)
        {
            case PostMessageFailedBehavior.AddToDLQ:
                {
                    //Add to DLQ
                    AddToDeadLetterQueue(subscriberId, message);

                    // Commit and move on
                    Commit(subscriberId);
                }
                break;
            case PostMessageFailedBehavior.Commit:
                {
                    // Commit and move on
                    Commit(subscriberId);
                }
                break;
            case PostMessageFailedBehavior.RetryOnceThenCommit:
                {
                    // manually retry once
                    await cursor.Handler.OnMessageReceiveAsync(message, subscriberId, _cts.Token);

                    // Commit and move on
                    Commit(subscriberId);
                }
                break;
            case PostMessageFailedBehavior.RetryOnceThenDLQ:
                {
                    // manually retry once
                    await cursor.Handler.OnMessageReceiveAsync(message, subscriberId, _cts.Token);

                    //Add to DLQ
                    AddToDeadLetterQueue(subscriberId, message);

                    // Commit and move on
                    Commit(subscriberId);
                }
                break;
            default:
            case PostMessageFailedBehavior.Block:
                break;
        }
    }

    private void AddToDeadLetterQueue(Guid subscriberId, MessageEnvelope message)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            cursor.DeadLetterQueue.Add(message);
            _logger.LogDebug("Subscriber {SubscriberId} added message {MessageId} to DLQ", subscriberId, message.Id);
        }
        else
        {
            _logger.LogWarning("Attempted to add to DLQ for non-existent subscriber {SubscriberId}", subscriberId);
        }
    }

    private void Commit(Guid subscriberId)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            cursor.IsCommitted = true;
            cursor.Index++;

            _logger.LogDebug("Subscriber {SubscriberId} committed message at index {Index}", subscriberId, cursor.Index - 1);
        }
        else
        {
            _logger.LogWarning("Attempted to commit for non-existent subscriber {SubscriberId}", subscriberId);
        }
    }

    private async Task DispatchLoopAsync(Guid subscriberId, CursorState cursor)
    {
        while (!cursor.Cancellation.Token.IsCancellationRequested)
        {
            MessageEnvelope? next;
            lock (_lock)
            {
                next = cursor.Index < _buffer.Count ? _buffer[cursor.Index] : null;
            }

            if (next != null)
            {
                _logger.LogDebug("Dispatching message {MessageId} to subscriber {SubscriberId} at index {Index}", next.Id, subscriberId, cursor.Index);

                var context = new Context
                {
                    [ContextItemSubscriberId] = subscriberId,
                    [ContextItemCursorState] = cursor,
                    [ContextItemMessage] = next
                };

                var result = await _retryPolicy.ExecuteAsync(ctx => cursor.Handler.OnMessageReceiveAsync(next, subscriberId, cursor.Cancellation.Token), context);

                if (result == DeliveryResult.Ack)
                {
                    Commit(subscriberId);
                }
            }
            else
            {
                await Task.Delay(100, cursor.Cancellation.Token); // backoff
            }
        }
    }
}
