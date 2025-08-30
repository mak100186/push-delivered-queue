using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Extensions.Logging;

namespace PushDeliveredQueue.FunctionalTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration
            config.AddJsonFile("appsettings.test.json", optional: true);

            // Add in-memory configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:01:00",
                ["SubscribableQueue:RetryCount"] = "2",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "100",
                ["Serilog:MinimumLevel:Default"] = "Information",
                ["Serilog:MinimumLevel:Override:Microsoft"] = "Warning",
                ["Serilog:MinimumLevel:Override:System"] = "Warning"
            });
        });

        builder.ConfigureLogging((context, logging) =>
        {
            // Clear default providers
            logging.ClearProviders();

            // Add Serilog
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(context.Configuration)
                .CreateLogger();

            logging.AddProvider(new SerilogLoggerProvider(logger));
        });

        builder.ConfigureServices(services =>
        {
            // Add any test-specific service configurations here
        });
    }
}
