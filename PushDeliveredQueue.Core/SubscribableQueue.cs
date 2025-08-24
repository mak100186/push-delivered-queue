using System.Collections.Concurrent;

using Polly;
using Polly.Retry;

using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core;

public class SubscribableQueue : IDisposable
{
    private readonly List<MessageEnvelope> _buffer = [];
    private readonly ConcurrentDictionary<Guid, CursorState> _subscribers = new();
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly AsyncRetryPolicy<DeliveryResult> _retryPolicy;
    private readonly TimeSpan _ttl;

    public SubscribableQueue(SubscribableQueueOptions options)
    {
        _ttl = options.Ttl;
        _retryPolicy = Policy
            .Handle<Exception>().OrResult<DeliveryResult>(r => r == DeliveryResult.Nack)
            .WaitAndRetryAsync(options.RetryCount, attempt => TimeSpan.FromMilliseconds(options.DelayBetweenRetriesMs * attempt));

        TriggerPruneInBackground();
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
                var result = await _retryPolicy.ExecuteAsync(() => cursor.Handler!(next));

                if (result == DeliveryResult.Ack)
                {
                    Commit(subscriberId);
                }
                // Nack is retried by Polly
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
