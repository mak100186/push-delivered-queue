using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.API.Handlers;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, conf) => conf.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen();

builder.Services.AddSubscribableQueueWithOptions(builder.Configuration);

builder.Services.AddScoped<SubscribedMessageHandler>();

var app = builder.Build();

// Subscribe to host lifetime events
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("PushDeliveredQueue API service has started successfully");
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("PushDeliveredQueue API service is stopping");
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PushDeliveredQueue API V1");
        c.EnableTryItOutByDefault();
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();

public partial class Program;
