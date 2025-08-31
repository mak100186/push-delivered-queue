using System.Diagnostics;
using System.Net.Sockets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
            Log.Information("PushDeliveredQueue Launcher Starting...");
            Log.Information("PushDeliveredQueue Launcher");
            Log.Information("================================");
            Log.Information(NewLine);

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

            if (noBuild)
            {
                Log.Information("Mode: Running without building projects (--no-build)");
            }
            else
            {
                Log.Information("Mode: Building and running projects");
            }
            Log.Information("Configuration: {Configuration}", buildConfiguration);
            Log.Information(NewLine);

            await launcher.StartAllProjectsAsync(noBuild, buildConfiguration);

            Log.Information(NewLine);
            Log.Information("All projects started successfully!");
            Log.Information("URLs:");
            Log.Information("   API: {ApiUrl}", apiUrl);
            Log.Information("   UI:  {UiUrl}", uiUrl);
            Log.Information("   Swagger: {ApiUrl}", apiUrl);
            Log.Information(NewLine);
            Log.Information("Press Ctrl+C to stop all projects...");

            // Keep the launcher running
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
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

public class ProjectLauncher
{
    private readonly ILogger<ProjectLauncher> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<Process> _processes = new();

    public ProjectLauncher(IConfiguration configuration)
    {
        _configuration = configuration;

        // Create logger factory with Serilog
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        _logger = loggerFactory.CreateLogger<ProjectLauncher>();
    }

    public async Task StartAllProjectsAsync(bool noBuild = false, string configuration = "Debug")
    {
        try
        {
            var apiUrl = GetApiUrl();
            var uiUrl = GetUiUrl();

            // Check port availability
            await CheckPortAvailabilityAsync(apiUrl, "API");
            await CheckPortAvailabilityAsync(uiUrl, "UI");

            // Build projects if needed
            if (!noBuild)
            {
                await BuildProjectAsync("PushDeliveredQueue.API", "API", configuration);
                await BuildProjectAsync("PushDeliveredQueue.UI", "UI", configuration);
            }

            // Start API first
            await StartProjectAsync("PushDeliveredQueue.API", "API", apiUrl, configuration);

            // Wait for API to be ready
            await Task.Delay(3000);

            // Start UI
            await StartProjectAsync("PushDeliveredQueue.UI", "UI", uiUrl, configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start projects");
            await StopAllProjectsAsync();
            throw;
        }
    }

    private async Task CheckPortAvailabilityAsync(string url, string projectName)
    {
        var uri = new Uri(url);
        var port = uri.Port;

        // Check if port is occupied
        if (await IsPortOccupiedAsync(port))
        {
            _logger.LogWarning("Port {Port} is occupied. Attempting to kill the process using it...", port, projectName);

            // Try to kill the process using the port
            await KillProcessUsingPortAsync(port, projectName);

            // Wait a moment for the process to be killed
            await Task.Delay(5000);

            // Check again
            if (await IsPortOccupiedAsync(port))
            {
                var errorMessage = $"Port {port} is still occupied after attempting to kill the process. {projectName} cannot start. Please manually free the port and try again.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            else
            {
                _logger.LogInformation("Port {Port} is now available for {ProjectName} after killing the previous process", port, projectName);
            }
        }
        else
        {
            _logger.LogInformation("Port {Port} is available for {ProjectName}", port, projectName);
        }
    }

    private async Task<bool> IsPortOccupiedAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            client.Close();
            return true; // Port is occupied
        }
        catch (SocketException)
        {
            return false; // Port is available
        }
    }

    private async Task KillProcessUsingPortAsync(int port, string projectName)
    {
        try
        {
            _logger.LogInformation("Attempting to kill process using port {Port} for {ProjectName}...", port, projectName);

            // Get the process ID using the port
            var processId = await GetProcessIdUsingPort(port);

            if (processId.HasValue)
            {
                var process = Process.GetProcessById(processId.Value);
                var processName = process.ProcessName;

                _logger.LogInformation("Killing process {ProcessName} (PID: {ProcessId}) using port {Port}", processName, processId.Value, port);

                process.Kill();
                await process.WaitForExitAsync();

                _logger.LogInformation("Successfully killed process {ProcessName} (PID: {ProcessId})", processName, processId.Value);
            }
            else
            {
                _logger.LogWarning("Could not find process using port {Port}", port);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill process using port {Port}", port);
        }
    }

    private async Task<int?> GetProcessIdUsingPort(int port)
    {
        try
        {
            // Use netstat to find the process using the port
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = $"-ano | findstr :{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse the output to find the PID
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Look for lines containing the port number and LISTENING state
                if (line.Contains($":{port}") && line.Contains("LISTENING"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    // The PID is typically the last part of the line
                    if (parts.Length > 0 && int.TryParse(parts[^1], out var pid))
                    {
                        return pid;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get process ID for port {Port}", port);
        }

        return null;
    }

    private async Task BuildProjectAsync(string projectPath, string projectName, string configuration)
    {
        _logger.LogInformation("Building {ProjectName} in {Configuration} configuration...", projectName, configuration);

        // Get the solution directory
        var solutionDir = GetSolutionDirectory();
        var fullProjectPath = Path.Combine(solutionDir, projectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{fullProjectPath}\" --configuration {configuration}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };

        // Set up output handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("[{ProjectName} Build] {Output}", projectName, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogError("[{ProjectName} Build] {Error}", projectName, e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start build for {projectName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{projectName} build failed with exit code {process.ExitCode}");
        }

        _logger.LogInformation("{ProjectName} built successfully", projectName);
    }

    private async Task StartProjectAsync(string projectPath, string projectName, string urls, string configuration)
    {
        _logger.LogInformation("Starting {ProjectName} on {Urls}...", projectName, urls);

        // Get the solution directory
        var solutionDir = GetSolutionDirectory();
        var fullProjectPath = Path.Combine(solutionDir, projectPath);

        // Construct path to the compiled DLL
        var dllPath = Path.Combine(fullProjectPath, "bin", configuration, "net9.0", $"{projectPath}.dll");

        _logger.LogInformation("DLL path: {DllPath}", dllPath);

        // Check if DLL exists
        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException($"Compiled DLL not found: {dllPath}. Please build the project first.");
        }

        var arguments = $"\"{dllPath}\" --urls {urls}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        var process = new Process { StartInfo = startInfo };
        _processes.Add(process);

        // Set up output handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("[{ProjectName}] {Output}", projectName, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogError("[{ProjectName}] {Error}", projectName, e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {projectName}");
        }

        // Begin reading output
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait a moment for the process to initialize
        await Task.Delay(2000);

        // Check if process has exited
        if (process.HasExited)
        {
            var exitCode = process.ExitCode;
            throw new InvalidOperationException($"{projectName} exited with code {exitCode}. Check the logs above for details.");
        }

        _logger.LogInformation("{ProjectName} started successfully", projectName);
    }

    private async Task StopAllProjectsAsync()
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

    public string GetApiUrl()
    {
        var apiUrl = _configuration["ApplicationUrls:Api"];
        return string.IsNullOrEmpty(apiUrl)
            ? throw new InvalidOperationException("ApplicationUrls:Api configuration is missing from appsettings.json")
            : apiUrl;
    }

    public string GetUiUrl()
    {
        var uiUrl = _configuration["ApplicationUrls:Ui"];
        return string.IsNullOrEmpty(uiUrl)
            ? throw new InvalidOperationException("ApplicationUrls:Ui configuration is missing from appsettings.json")
            : uiUrl;
    }

    private static string GetSolutionDirectory()
    {
        // Navigate up from current directory to find solution root
        var currentDir = Directory.GetCurrentDirectory();

        // If we're in the launcher's bin directory, go up to solution root
        if (currentDir.Contains("PushDeliveredQueue.Launcher\\bin"))
        {
            // Go up: bin/Debug/net9.0 -> bin/Debug -> bin -> PushDeliveredQueue.Launcher -> solution root
            var solutionDir = Directory.GetParent(currentDir)?.Parent?.Parent?.Parent?.FullName;
            return string.IsNullOrEmpty(solutionDir) ? throw new InvalidOperationException("Could not determine solution directory") : solutionDir;
        }

        // If we're already in the solution root
        return File.Exists(Path.Combine(currentDir, "PushDeliveredQueue.sln")) ? currentDir : throw new InvalidOperationException("Could not determine solution directory");
    }
}
