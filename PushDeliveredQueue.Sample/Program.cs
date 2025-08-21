using PushDeliveredQueue.AspNetCore.DependencyInjection;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, conf) => conf.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen();

builder.Services.AddSubscribableQueueWithOptions(builder.Configuration);

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