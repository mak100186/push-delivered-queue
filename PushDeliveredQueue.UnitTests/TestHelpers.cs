using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.Core.Configs;

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
    public static ServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddConsole(); // Add other providers as needed
            config.SetMinimumLevel(LogLevel.Information);
        });

        return services;
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
        var services = GetServiceCollection();
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
}
