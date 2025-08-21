namespace PushDeliveredQueue.Core.Configs;

/* Add the following JSON configuration to your appsettings.json file:
 "SubscribableQueue": {
  "Ttl": "00:00:30",
  "RetryCount": 5,
  "DelayBetweenRetriesMs": 200
}
 */

public class SubscribableQueueOptions
{
    public TimeSpan Ttl { get; set; } = TimeSpan.FromSeconds(30);
    public int RetryCount { get; set; } = 3;
    public int DelayBetweenRetriesMs { get; set; } = 100;
}

