using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PushDeliveredQueue.AspNetCore.Configs;
using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;

namespace PushDeliveredQueue.AspNetCore.DependencyInjection;

public static class SubscribableQueueOptionsExtensions
{
    public static IServiceCollection AddSubscribableQueueWithOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new SubscribableQueueOptions();
        configuration.GetSection("SubscribableQueue").Bind(options);
        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton(sp =>
        {
            var queueOptions = sp.GetRequiredService<IOptions<SubscribableQueueOptions>>().Value;
            return new SubscribableQueue(queueOptions);
        });

        return services;
    }
}