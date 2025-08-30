using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Wrap;

using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

using static PushDeliveredQueue.Core.Constants.SubscribableQueueConstants;

namespace PushDeliveredQueue.Core;

public class SubscribableQueue : IDisposable
{
    private readonly List<MessageEnvelope> _buffer = [];
    private readonly ConcurrentDictionary<Guid, CursorState> _subscribers = new();
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly AsyncPolicyWrap<DeliveryResult> _retryPolicy;
    private readonly TimeSpan _ttl;
    private readonly ILogger<SubscribableQueue> _logger;

    public SubscribableQueue(IOptions<SubscribableQueueOptions> options, ILogger<SubscribableQueue> logger)
    {
        _ttl = options.Value.Ttl;
        _logger = logger;

        var fallbackPolicy = Policy<DeliveryResult>
            .Handle<Exception>()
            .OrResult(r => r == DeliveryResult.Nack)
            .FallbackAsync(
                fallbackValue: DeliveryResult.Nack,
                onFallbackAsync: async (result, context) => await OnFailedAsync(result, context));

        var retryPolicy = Policy<DeliveryResult>
            .Handle<Exception>()
            .OrResult(r => r == DeliveryResult.Nack)
            .WaitAndRetryAsync(options.Value.RetryCount, _ => TimeSpan.FromMilliseconds(options.Value.DelayBetweenRetriesMs));

        _retryPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy);

        Task.Run(() => PruneExpiredMessages(_cts.Token), _cts.Token);
    }

    private async Task OnFailedAsync(DelegateResult<DeliveryResult> result, Context context)
    {
        var subscriberId = Guid.Parse(context[ContextItemSubscriberId]?.ToString()!);
        var cursor = (CursorState)context[ContextItemCursorState]!;
        var message = (MessageEnvelope)context[ContextItemMessage]!;

        _logger.LogInformation("Message delivery failed. Invoking OnMessageFailedHandler. {SubscriberId} {@Message}", subscriberId, message);

        var postMessageFailedBehavior = await cursor.Handler.OnMessageFailedHandlerAsync(message, subscriberId);

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
                    await cursor.Handler.OnMessageReceiveAsync(message, subscriberId);

                    // Commit and move on
                    Commit(subscriberId);
                }
                break;
            case PostMessageFailedBehavior.RetryOnceThenDLQ:
                {
                    // manually retry once
                    await cursor.Handler.OnMessageReceiveAsync(message, subscriberId);

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

    public SubscribableQueueState GetState()
    {
        var state = new SubscribableQueueState
        {
            Ttl = _ttl
        };

        lock (_lock)
        {
            state.Buffer = _buffer
                .Select(m => new MessageEnvelope(m.Id, m.CreatedAt, m.Payload))
                .ToList();
        }

        foreach (var kvp in _subscribers)
        {
            state.Subscribers[kvp.Key] = new SubscriberState
            {
                CursorIndex = kvp.Value.Index,
                IsCommitted = kvp.Value.IsCommitted,
                PendingCount = Math.Max(state.Buffer.Count - (kvp.Value.Index + 1), 0),
                DeadLetterQueue = kvp.Value.DeadLetterQueue
                    .Select(m => new MessageEnvelope(m.Id, m.CreatedAt, m.Payload))
                    .ToList()
            };
        }

        return state;
    }

    public string Enqueue(string payload)
    {
        var messageId = Guid.NewGuid();
        lock (_lock)
        {
            _buffer.Add(new MessageEnvelope(messageId, DateTime.UtcNow, payload));
        }

        return messageId.ToString();
    }

    public Guid Subscribe(IQueueEventHandler handler)
    {
        var subscriberId = Guid.NewGuid();
        var cursor = new CursorState
        {
            Handler = handler
        };
        _subscribers.TryAdd(subscriberId, cursor);

        Task.Run(() => DispatchLoopAsync(subscriberId, cursor), CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cursor.Cancellation.Token).Token);

        return subscriberId;
    }

    public void Unsubscribe(Guid subscriberId)
    {
        if (_subscribers.TryRemove(subscriberId, out var cursor))
        {
            cursor.Cancellation.Cancel();
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

                var result = await _retryPolicy.ExecuteAsync(ctx => cursor.Handler.OnMessageReceiveAsync(next, subscriberId), context);

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
