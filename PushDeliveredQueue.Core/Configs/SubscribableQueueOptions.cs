using System.ComponentModel.DataAnnotations;

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
    [Required]
    public TimeSpan Ttl { get; set; }
    [Range(1, 100)]
    public int RetryCount { get; set; }
    [Range(10, 1000)]
    public int DelayBetweenRetriesMs { get; set; }
}

