using PushDeliveredQueue.API.Handlers;
using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.UI.Components;
using PushDeliveredQueue.UI.Services;

namespace PushDeliveredQueue.UI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add queue services
        builder.Services.AddSubscribableQueueWithOptions(builder.Configuration);
        builder.Services.AddScoped<SubscribedMessageHandler>();

        // Add HTTP client for API communication
        var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? throw new InvalidOperationException("ApiSettings:BaseUrl configuration is missing from appsettings.json");
        builder.Services.AddHttpClient<QueueApiService>(client => client.BaseAddress = new Uri(apiBaseUrl));

        // Add queue monitoring service
        builder.Services.AddScoped<QueueMonitoringService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
