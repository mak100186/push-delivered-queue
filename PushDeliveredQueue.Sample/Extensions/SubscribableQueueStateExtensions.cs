using PushDeliveredQueue.Core.Models;
using PushDeliveredQueue.Sample.Dtos;

namespace PushDeliveredQueue.Sample.Extensions;

public static class SubscribableQueueStateExtensions
{
    public static SubscribableQueueStateDto ToExternalDto(this SubscribableQueueState state)
    {
        return new SubscribableQueueStateDto
        {
            Buffer = state.Buffer.Select(m => new MessageEnvelopeDto(m.Id, m.Payload, m.CreatedAt.GetTimeUntilExpiry(state.Ttl).FormatRelative())).ToList(),
            Subscribers = state.Subscribers.ToDictionary(
                kvp => kvp.Key,
                kvp => new SubscriberStateDto()
                {
                    IsBlocked = !kvp.Value.IsCommitted,
                    PendingMessageCount = kvp.Value.PendingCount
                })
        };
    }
}
