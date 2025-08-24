using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

namespace PushDeliveredQueue.UnitTests;

public static class TestHelpers
{
    public static SubscribableQueueOptions CreateValidOptions()
    {
        return new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 200
        };
    }

    public static SubscribableQueueOptions CreateOptionsWithCustomValues(TimeSpan ttl, int retryCount, int delayMs)
    {
        return new SubscribableQueueOptions
        {
            Ttl = ttl,
            RetryCount = retryCount,
            DelayBetweenRetriesMs = delayMs
        };
    }

    public static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    public static IConfiguration CreateValidConfiguration()
    {
        return CreateConfiguration(new Dictionary<string, string?>
        {
            ["SubscribableQueue:Ttl"] = "00:05:00",
            ["SubscribableQueue:RetryCount"] = "3",
            ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
        });
    }

    public static IServiceProvider CreateServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSubscribableQueueWithOptions(configuration);
        return services.BuildServiceProvider();
    }

    public static IServiceProvider CreateServiceProviderWithValidConfiguration()
    {
        return CreateServiceProvider(CreateValidConfiguration());
    }

    public static async Task WaitForCondition(Func<bool> condition, int timeoutMs = 5000, int intervalMs = 100)
    {
        var startTime = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            await Task.Delay(intervalMs);
        }
    }

    public static async Task WaitForMessageDelivery(int expectedCount, List<string> deliveredMessages, int timeoutMs = 5000)
    {
        await WaitForCondition(() => deliveredMessages.Count >= expectedCount, timeoutMs);
    }

    public static MessageHandler CreateAckHandler(List<string> deliveredMessages)
    {
        return message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };
    }

    public static MessageHandler CreateNackHandler(List<string> deliveredMessages)
    {
        return message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Nack);
        };
    }

    public static MessageHandler CreateExceptionHandler(List<string> deliveredMessages, string exceptionMessage = "Test exception")
    {
        return message =>
        {
            deliveredMessages.Add(message.Payload);
            throw new InvalidOperationException(exceptionMessage);
        };
    }

    public static MessageHandler CreateRetryHandler(List<string> deliveredMessages, int successAfterAttempts)
    {
        var attemptCount = 0;
        return message =>
        {
            attemptCount++;
            deliveredMessages.Add($"{message.Payload}-attempt-{attemptCount}");
            
            if (attemptCount >= successAfterAttempts)
            {
                return Task.FromResult(DeliveryResult.Ack);
            }
            
            return Task.FromResult(DeliveryResult.Nack);
        };
    }
}
