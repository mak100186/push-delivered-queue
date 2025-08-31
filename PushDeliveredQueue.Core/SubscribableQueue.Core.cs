using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Wrap;

using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.Core;

public partial class SubscribableQueue
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
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

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

    public Guid Enqueue(string payload)
    {
        lock (_lock)
        {
            var messageId = Guid.NewGuid();

            _buffer.Add(new MessageEnvelope(messageId, DateTime.UtcNow, payload));

            return messageId;
        }
    }

    public Guid Subscribe(IQueueEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

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
}
