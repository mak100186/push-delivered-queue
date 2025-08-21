using PushDeliveredQueue.Core.Configs;

namespace PushDeliveredQueue.AspNetCore.Configs;

public static class SubscribableQueueOptionsExtensions
{
    public static void Validate(this SubscribableQueueOptions options)
    {
        if (options.Ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Ttl), "TTL must be greater than zero.");
        }

        if (options.RetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.RetryCount), "Retry count cannot be negative.");
        }

        if (options.DelayBetweenRetriesMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.DelayBetweenRetriesMs), "Delay between retries cannot be negative.");
        }
    }
}