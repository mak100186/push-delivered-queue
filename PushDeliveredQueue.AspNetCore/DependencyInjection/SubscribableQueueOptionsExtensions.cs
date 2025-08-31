using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;

using static PushDeliveredQueue.Core.Constants.SubscribableQueueConstants;

namespace PushDeliveredQueue.AspNetCore.DependencyInjection;

public static class SubscribableQueueOptionsExtensions
{
    public static IServiceCollection AddSubscribableQueueWithOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SubscribableQueueOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<SubscribableQueue>();

        return services;
    }
}