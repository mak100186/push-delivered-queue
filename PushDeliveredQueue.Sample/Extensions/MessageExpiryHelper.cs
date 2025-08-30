namespace PushDeliveredQueue.Sample.Extensions;

public static class MessageExpiryHelper
{
    public static TimeSpan GetTimeUntilExpiry(this DateTime createdAtUtc, TimeSpan ttl)
    {
        var expiryTime = createdAtUtc + ttl;
        var now = DateTime.UtcNow;

        return expiryTime > now
            ? expiryTime - now
            : TimeSpan.Zero; // Already expired
    }
}
