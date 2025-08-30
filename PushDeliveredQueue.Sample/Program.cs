using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.Sample.Handlers;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, conf) => conf.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen();

builder.Services.AddSubscribableQueueWithOptions(builder.Configuration);

builder.Services.AddScoped<SubscribedMessageHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PushDeliveredQueue Sample API V1");
        c.EnableTryItOutByDefault();
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();

public partial class Program;
