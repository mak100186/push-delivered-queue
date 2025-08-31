using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PushDeliveredQueue.UnitTests;

public static class TestHelpers
{
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
}
