using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;

namespace PushDeliveredQueue.AspNetCore.DependencyInjection;

public static class SubscribableQueueOptionsExtensions
{
    public static IServiceCollection AddSubscribableQueueWithOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SubscribableQueueOptions>("SubscribableQueue");
        services.AddSingleton<SubscribableQueue>();

        return services;
    }
}
