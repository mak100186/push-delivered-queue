@echo off
echo 🚀 Launching PushDeliveredQueue API and UI...

echo 📡 Starting API on https://localhost:7246...
start "PushDeliveredQueue - API" cmd /k "cd PushDeliveredQueue.API && dotnet run --urls https://localhost:7246"

echo ⏳ Waiting for API to start...
timeout /t 3 /nobreak >nul

echo 🖥️  Starting UI on https://localhost:7274...
start "PushDeliveredQueue - UI" cmd /k "cd PushDeliveredQueue.UI && dotnet run --urls https://localhost:7274"

echo ✅ Both projects are starting...
echo 📋 URLs:
echo    API: https://localhost:7246
echo    UI:  https://localhost:7274
echo    Swagger: https://localhost:7246
echo.
echo ⏳ Please wait for both applications to fully start...
pause
