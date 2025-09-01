using Microsoft.Extensions.Configuration;

using Serilog;

using static System.Environment;

namespace PushDeliveredQueue.Launcher;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetLauncherDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("[LCH] PushDeliveredQueue Launcher v1.0.0");
            Log.Information("[LCH] ==================================");

            var launcher = new ProjectLauncher(configuration);
            var apiUrl = launcher.GetApiUrl();
            var uiUrl = launcher.GetUiUrl();

            // Check if --no-build flag is provided
            var noBuild = args.Contains("--no-build");

            // Check for configuration (default to Debug)
            var buildConfiguration = "Debug";
            if (args.Contains("--configuration") || args.Contains("-c"))
            {
                var configIndex = Array.IndexOf(args, "--configuration");
                if (configIndex == -1) configIndex = Array.IndexOf(args, "-c");

                if (configIndex >= 0 && configIndex + 1 < args.Length)
                {
                    buildConfiguration = args[configIndex + 1];
                }
            }

            Log.Information("[LCH] {Mode}", noBuild ? "Mode: Running without building projects (--no-build)" : "Mode: Building and running projects");
            Log.Information("[LCH] Configuration: {Configuration}", buildConfiguration);
            Log.Information("[LCH] {NewLine}", NewLine);

            await launcher.StartAllProjectsAsync(noBuild, buildConfiguration);

            Log.Information("[LCH] {NewLine}", NewLine);
            Log.Information("[LCH] All projects started successfully!");
            Log.Information("[LCH] URLs:");
            Log.Information("[LCH]    API: {ApiUrl}", apiUrl);
            Log.Information("[LCH]    UI:  {UiUrl}", uiUrl);
            Log.Information("[LCH] {NewLine}", NewLine);
            
            // Open browser with the URLs
            await launcher.OpenBrowserUrlsAsync(apiUrl, uiUrl);
            
            Log.Information("[LCH] Press Ctrl+C to stop all projects...");

            // Keep the launcher running
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[LCH] Application terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static string GetLauncherDirectory()
    {
        // Get the directory where the launcher executable is located
        var currentDir = Directory.GetCurrentDirectory();

        // If we're in the launcher's bin directory, go up to the launcher project directory
        if (currentDir.Contains("PushDeliveredQueue.Launcher\\bin"))
        {
            // Go up: bin/Debug/net9.0 -> bin/Debug -> bin -> PushDeliveredQueue.Launcher
            var launcherDir = Directory.GetParent(currentDir)?.Parent?.Parent?.FullName;
            if (string.IsNullOrEmpty(launcherDir))
            {
                throw new InvalidOperationException("Could not determine launcher directory");
            }
            return launcherDir;
        }

        // If we're already in the launcher project directory
        if (File.Exists(Path.Combine(currentDir, "PushDeliveredQueue.Launcher.csproj")))
        {
            return currentDir;
        }

        // If we're in the solution root, go to the launcher directory
        if (File.Exists(Path.Combine(currentDir, "PushDeliveredQueue.sln")))
        {
            var launcherDir = Path.Combine(currentDir, "PushDeliveredQueue.Launcher");
            if (Directory.Exists(launcherDir))
            {
                return launcherDir;
            }
        }

        throw new InvalidOperationException("Could not determine launcher directory");
    }
}
