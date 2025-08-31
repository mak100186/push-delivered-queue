# PushDeliveredQueue Launcher

A simple launcher application that starts both the API and UI projects simultaneously.

## Usage

### Option 1: Using the Launcher Project

```bash
# Build and run the launcher
dotnet run --project PushDeliveredQueue.Launcher
```

This will:
1. Start the API on `https://localhost:7246`
2. Wait 3 seconds for the API to initialize
3. Start the UI on `https://localhost:7274`
4. Keep the launcher running to manage both processes

### Option 2: Using PowerShell Script

```powershell
# Run the PowerShell script
.\launch-both.ps1
```

### Option 3: Using Batch File

```cmd
# Run the batch file
launch-both.bat
```

## Features

- **Automatic Startup**: Starts both projects in the correct order
- **Process Management**: Manages the lifecycle of both applications
- **Graceful Shutdown**: Stops all processes when the launcher is terminated
- **Cross-Platform**: Works on Windows, macOS, and Linux

## URLs

Once both projects are running:

- **API**: https://localhost:7246
- **UI**: https://localhost:7274
- **Swagger Documentation**: https://localhost:7246

## Stopping the Applications

Press `Ctrl+C` in the launcher terminal to stop both applications gracefully.
