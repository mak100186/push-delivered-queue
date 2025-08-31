# Define paths to your service projects
$service1Path = ".\PushDeliveredQueue.API"
$service2Path = ".\PushDeliveredQueue.UI"

# Optional: Set window titles for clarity
$service1Title = "PushDeliveredQueue.Api"
$service2Title = "PushDeliveredQueue.UI"

# Launch Service 1
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run" -WindowStyle Normal -WorkingDirectory $service1Path

# Launch Service 2
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run" -WindowStyle Normal -WorkingDirectory $service2Path

# Launch default browser with hardcoded URLs
Start-Process "http://localhost:5155/index.html"
Start-Process "http://localhost:5256/"
