# PushDeliveredQueue Launch Script
# This script launches both the API and UI projects simultaneously

Write-Host "🚀 Launching PushDeliveredQueue API and UI..." -ForegroundColor Green

# Function to start a project in a new terminal window
function Start-ProjectInNewTerminal {
    param(
        [string]$ProjectPath,
        [string]$ProjectName,
        [string]$Urls
    )
    
    $command = "cd '$ProjectPath' && dotnet run --urls '$Urls'"
    $title = "PushDeliveredQueue - $ProjectName"
    
    # Start in a new PowerShell window
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $command -WindowStyle Normal
}

# Start the API project
Write-Host "📡 Starting API on https://localhost:7246..." -ForegroundColor Yellow
Start-ProjectInNewTerminal -ProjectPath "PushDeliveredQueue.API" -ProjectName "API" -Urls "https://localhost:7246"

# Wait a moment for the API to start
Start-Sleep -Seconds 3

# Start the UI project
Write-Host "🖥️  Starting UI on https://localhost:7274..." -ForegroundColor Yellow
Start-ProjectInNewTerminal -ProjectPath "PushDeliveredQueue.UI" -ProjectName "UI" -Urls "https://localhost:7274"

Write-Host "✅ Both projects are starting..." -ForegroundColor Green
Write-Host "📋 URLs:" -ForegroundColor Cyan
Write-Host "   API: https://localhost:7246" -ForegroundColor White
Write-Host "   UI:  https://localhost:7274" -ForegroundColor White
Write-Host "   Swagger: https://localhost:7246" -ForegroundColor White
Write-Host ""
Write-Host "⏳ Please wait for both applications to fully start..." -ForegroundColor Yellow
