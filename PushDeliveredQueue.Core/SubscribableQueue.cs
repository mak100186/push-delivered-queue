using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using Polly;
using Polly.Wrap;

using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core;

public class SubscribableQueue : IDisposable
{
    private readonly List<MessageEnvelope> _buffer = [];
    private readonly ConcurrentDictionary<Guid, CursorState> _subscribers = new();
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly AsyncPolicyWrap<DeliveryResult> _retryPolicy;
    private readonly TimeSpan _ttl;

    public SubscribableQueue(IOptions<SubscribableQueueOptions> options)
    {
        _ttl = options.Value.Ttl;

        var fallbackPolicy = Policy<DeliveryResult>
            .Handle<Exception>()
            .OrResult(r => r == DeliveryResult.Nack)
            .FallbackAsync(
                fallbackValue: DeliveryResult.Nack,
                onFallbackAsync: async (result, context) => await OnFailed(result, context));

        var retryPolicy = Policy<DeliveryResult>
            .Handle<Exception>()
            .OrResult(r => r == DeliveryResult.Nack)
            .WaitAndRetryAsync(options.Value.RetryCount, _ => TimeSpan.FromMilliseconds(options.Value.DelayBetweenRetriesMs));

        _retryPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy);

        TriggerPruneInBackground();
    }


    //when all retries are exhausted. This should be made configurable:
    //Option 1: Retry and block
    //Option 2: Retry and continue
    private async Task OnFailed(DelegateResult<DeliveryResult> result, Context context)
    {
        var subscriberId = Guid.Parse(context["SubscriberId"]?.ToString()!);

        Commit(subscriberId); // or handle based on options
    }

    public SubscribableQueueState GetState()
    {
        var state = new SubscribableQueueState();

        lock (_lock)
        {
            state.Buffer = _buffer
                .Select(m => new MessageEnvelope(m.Id, m.Timestamp, m.Payload))
                .ToList();
        }

        foreach (var kvp in _subscribers)
        {
            state.Subscribers[kvp.Key] = new SubscriberState
            {
                CursorIndex = kvp.Value.Index,
                IsCommitted = kvp.Value.IsCommitted,
                PendingCount = state.Buffer.Count - (kvp.Value.Index + 1)
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

    public Guid Subscribe(MessageHandler handler)
    {
        var id = Guid.NewGuid();
        var cursor = new CursorState
        {
            Handler = handler
        };
        _subscribers.TryAdd(id, cursor);

        Task.Run(() => DispatchLoopAsync(id, cursor), CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cursor.Cancellation.Token).Token);

        return id;
    }

    public void Unsubscribe(Guid subscriberId)
    {
        if (_subscribers.TryRemove(subscriberId, out var cursor))
        {
            cursor.Cancellation.Cancel();
        }
    }

    private void Commit(Guid subscriberId)
    {
        if (_subscribers.TryGetValue(subscriberId, out var cursor))
        {
            cursor.IsCommitted = true;
            cursor.Index++;
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
                var context = new Context
                {
                    ["SubscriberId"] = subscriberId
                };

                var result = await _retryPolicy.ExecuteAsync(ctx => cursor.Handler!(next), context);

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

    private void TriggerPruneInBackground() => Task.Run(PruneExpiredMessages, _cts.Token);

    private void PruneExpiredMessages()
    {
        var cutoff = DateTime.UtcNow - _ttl;

        lock (_lock)
        {
            var removedCount = 0;

            while (_buffer.Count > 0 && _buffer[0].Timestamp < cutoff)
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

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        foreach (var sub in _subscribers.Values)
        {
            sub.Cancellation.Cancel();
        }
    }
}
