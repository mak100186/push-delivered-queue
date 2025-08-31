using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace PushDeliveredQueue.Launcher;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("PushDeliveredQueue Launcher");
        Console.WriteLine("================================");
        Console.WriteLine();

        var launcher = new ProjectLauncher();
        await launcher.StartAllProjectsAsync();

        Console.WriteLine();
        Console.WriteLine("All projects started successfully!");
        Console.WriteLine("URLs:");
        Console.WriteLine("   API: https://localhost:7246");
        Console.WriteLine("   UI:  https://localhost:7274");
        Console.WriteLine("   Swagger: https://localhost:7246");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop all projects...");

        // Keep the launcher running
        await Task.Delay(Timeout.Infinite);
    }
}

public class ProjectLauncher
{
    private readonly ILogger<ProjectLauncher> _logger;
    private readonly List<Process> _processes = new();

    public ProjectLauncher()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ProjectLauncher>();
    }

    public async Task StartAllProjectsAsync()
    {
        try
        {
            // Start API first
            await StartProjectAsync("PushDeliveredQueue.Sample", "API", "https://localhost:7246");
            
            // Wait for API to be ready
            await Task.Delay(3000);
            
            // Start UI
            await StartProjectAsync("PushDeliveredQueue.UI", "UI", "https://localhost:7274");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start projects");
            await StopAllProjectsAsync();
            throw;
        }
    }

    private async Task StartProjectAsync(string projectPath, string projectName, string urls)
    {
        _logger.LogInformation("Starting {ProjectName} on {Urls}...", projectName, urls);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {projectPath} --urls {urls}",
            UseShellExecute = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        var process = new Process { StartInfo = startInfo };
        _processes.Add(process);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {projectName}");
        }

        // Wait a moment for the process to initialize
        await Task.Delay(1000);

        _logger.LogInformation("{ProjectName} started successfully", projectName);
    }

    public async Task StopAllProjectsAsync()
    {
        _logger.LogInformation("Stopping all projects...");

        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop process {ProcessId}", process.Id);
            }
        }

        _processes.Clear();
        _logger.LogInformation("All projects stopped");
    }
}
